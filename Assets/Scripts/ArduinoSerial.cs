using System.IO.Ports;
using UnityEngine;
using UnityEngine.UI;

public class ArduinoSensorReader : MonoBehaviour
{
    [Header("=== ARDUINO SETTINGS ===")]
    public int baudRate = 9600;
    public bool autoConnect = true;
    public float dataReadInterval = 0.1f; // How often to check for new data
    
    [Header("=== UI TEXT ELEMENTS ===")]
    public Text temperatureText;
    public Text humidityText;
    public Text gasText;
    public Text connectionStatusText;
    public Text fanSpeedText;
    
    [Header("=== UI SLIDERS ===")]
    public Slider temperatureSlider;
    public Slider humiditySlider;
    public Slider gasSlider;
    
    [Header("=== FAN SETTINGS ===")]
    public GameObject fanBlade;
    public float maxFanSpeed = 800f;
    public float fanSmoothTime = 0.2f;
    
    [Header("=== SENSOR RANGES ===")]
    [Tooltip("Maximum expected temperature in Celsius")]
    public float maxTemperature = 50f;
    
    [Tooltip("Maximum expected gas sensor value")]
    public float maxGasValue = 1023f; // Default for Arduino analog input (0-1023)
    
    // Current sensor values (updated from Arduino)
    private float temperature = 0f;
    private float humidity = 0f;
    private float gasValue = 0f;
    
    // Arduino connection
    private SerialPort arduinoPort;
    private bool isConnected = false;
    private float timeSinceLastRead = 0f;
    
    // Fan control
    private float currentFanSpeed = 0f;
    private float targetFanSpeed = 0f;
    private float fanVelocity = 0f;
    
    // Last received raw data (for debugging)
    private string lastReceivedData = "";
    
    void Start()
    {
        InitializeUI();
        
        if (autoConnect)
        {
            FindAndConnectToArduino();
        }
    }
    
    void InitializeUI()
    {
        // Set initial slider values
        if (temperatureSlider != null)
        {
            temperatureSlider.value = temperature / maxTemperature;
        }
        
        if (humiditySlider != null)
        {
            humiditySlider.value = humidity / 100f;
        }
        
        if (gasSlider != null)
        {
            gasSlider.value = gasValue / maxGasValue;
        }
        
        UpdateUI();
        
        if (connectionStatusText != null)
        {
            connectionStatusText.text = "DISCONNECTED";
            connectionStatusText.color = Color.red;
        }
    }
    
    void FindAndConnectToArduino()
    {
        string[] ports = SerialPort.GetPortNames();
        
        if (ports.Length == 0)
        {
            Debug.LogWarning("No COM ports available. Arduino may not be connected.");
            UpdateConnectionStatus("No COM Ports Found", false);
            return;
        }
        
        Debug.Log("Available COM ports: " + string.Join(", ", ports));
        
        foreach (string port in ports)
        {
            Debug.Log("Trying to connect to: " + port);
            
            try
            {
                arduinoPort = new SerialPort(port, baudRate)
                {
                    ReadTimeout = 100,
                    DtrEnable = true,  // Important for Arduino auto-reset
                    RtsEnable = true
                };
                
                arduinoPort.Open();
                arduinoPort.DiscardInBuffer(); // Clear any old data
                
                isConnected = true;
                UpdateConnectionStatus("Connected: " + port, true);
                
                Debug.Log($"Successfully connected to Arduino on {port} at {baudRate} baud");
                return;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to connect on {port}: {e.Message}");
                
                if (arduinoPort != null && arduinoPort.IsOpen)
                {
                    arduinoPort.Close();
                }
            }
        }
        
        UpdateConnectionStatus("Arduino Not Found", false);
        Debug.LogWarning("Could not connect to Arduino on any port.");
    }
    
    void Update()
    {
        timeSinceLastRead += Time.deltaTime;
        
        if (timeSinceLastRead >= dataReadInterval)
        {
            ReadDataFromArduino();
            timeSinceLastRead = 0f;
        }
        
        UpdateFanSpeed();
        RotateFan();
    }
    
    void ReadDataFromArduino()
    {
        if (isConnected && arduinoPort != null && arduinoPort.IsOpen)
        {
            try
            {
                if (arduinoPort.BytesToRead > 0)
                {
                    string rawData = arduinoPort.ReadLine();
                    ProcessArduinoData(rawData);
                }
            }
            catch (System.TimeoutException)
            {
                // No data available yet - this is normal
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error reading from Arduino: {e.Message}");
                // Don't disconnect on error, just wait for next reading
            }
        }
        else
        {
            // Arduino not connected - use slider values
            UseSliderValues();
        }
    }
    
    void ProcessArduinoData(string rawData)
    {
        // Store for debugging
        lastReceivedData = rawData;
        
        // Clean the data
        rawData = rawData.Trim();
        
        // Check if data matches expected format: "H:90.0,T:27.9,G:169"
        if (string.IsNullOrEmpty(rawData) || rawData.Length < 10)
        {
            Debug.LogWarning($"Invalid data format: '{rawData}'");
            return;
        }
        
        Debug.Log($"Received from Arduino: {rawData}");
        
        try
        {
            // Split the data by commas
            string[] parts = rawData.Split(',');
            
            if (parts.Length != 3)
            {
                Debug.LogWarning($"Expected 3 parts, got {parts.Length}: {rawData}");
                return;
            }
            
            // Process each part
            foreach (string part in parts)
            {
                string cleanPart = part.Trim();
                
                if (cleanPart.StartsWith("H:"))
                {
                    // Format: "H:90.0"
                    string valueStr = cleanPart.Substring(2);
                    if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hValue))
                    {
                        humidity = Mathf.Clamp(hValue, 0f, 100f);
                    }
                }
                else if (cleanPart.StartsWith("T:"))
                {
                    // Format: "T:27.9"
                    string valueStr = cleanPart.Substring(2);
                    if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tValue))
                    {
                        temperature = Mathf.Clamp(tValue, 0f, maxTemperature);
                    }
                }
                else if (cleanPart.StartsWith("G:"))
                {
                    // Format: "G:169"
                    string valueStr = cleanPart.Substring(2);
                    if (float.TryParse(valueStr, out float gValue))
                    {
                        gasValue = Mathf.Clamp(gValue, 0f, maxGasValue);
                    }
                }
                else
                {
                    Debug.LogWarning($"Unknown data part: {cleanPart}");
                }
            }
            
            // Update UI with new values
            UpdateUI();
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing data '{rawData}': {e.Message}");
        }
    }
    
    void UseSliderValues()
    {
        // When Arduino is not connected, use slider values
        if (temperatureSlider != null)
        {
            temperature = temperatureSlider.value * maxTemperature;
        }
        
        if (humiditySlider != null)
        {
            humidity = humiditySlider.value * 100f;
        }
        
        if (gasSlider != null)
        {
            gasValue = gasSlider.value * maxGasValue;
        }
        
        UpdateUI();
    }
    
    void UpdateUI()
    {
        // Update all text displays
        if (temperatureText != null)
        {
            temperatureText.text = $"{temperature:F1}°C";
        }
        
        if (humidityText != null)
        {
            humidityText.text = $"{humidity:F1}%";
        }
        
        if (gasText != null)
        {
            gasText.text = $"{gasValue:F0}";
        }
        
        if (fanSpeedText != null)
        {
            fanSpeedText.text = $"{currentFanSpeed:F0} RPM";
        }
        
        // Auto-update sliders based on current values
        UpdateSlidersFromValues();
    }
    
    void UpdateSlidersFromValues()
    {
        // This makes sliders follow the actual sensor values
        if (temperatureSlider != null)
        {
            temperatureSlider.value = temperature / maxTemperature;
        }
        
        if (humiditySlider != null)
        {
            humiditySlider.value = humidity / 100f;
        }
        
        if (gasSlider != null)
        {
            gasSlider.value = gasValue / maxGasValue;
        }
    }
    
    void UpdateFanSpeed()
    {
        // Normalize all sensor values to 0-1 range
        float tempFactor = Mathf.Clamp01(temperature / maxTemperature);
        float humidityFactor = Mathf.Clamp01(humidity / 100f);
        float gasFactor = Mathf.Clamp01(gasValue / maxGasValue);
        
        // Calculate weighted average (all sensors contribute equally)
        float averageFactor = (tempFactor + humidityFactor + gasFactor) / 3f;
        
        // Set target fan speed based on average
        targetFanSpeed = averageFactor * maxFanSpeed;
        
        // Smoothly transition to target speed
        currentFanSpeed = Mathf.SmoothDamp(
            currentFanSpeed,
            targetFanSpeed,
            ref fanVelocity,
            fanSmoothTime
        );
    }
    
    void RotateFan()
    {
        if (fanBlade == null)
        {
            Debug.LogWarning("Fan blade GameObject is not assigned!");
            return;
        }
        
        // Rotate the fan around its Y-axis
        // 6 degrees per RPM is a good realistic ratio
        float rotationAngle = currentFanSpeed * 6f * Time.deltaTime;
        fanBlade.transform.Rotate(0, rotationAngle, 0);
    }
    
    void UpdateConnectionStatus(string message, bool connected)
    {
        isConnected = connected;
        
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
            connectionStatusText.color = connected ? Color.green : Color.red;
        }
    }
    
    // ===== PUBLIC METHODS FOR UI BUTTONS =====
    
    public void ConnectButton()
    {
        if (!isConnected)
        {
            FindAndConnectToArduino();
        }
    }
    
    public void DisconnectButton()
    {
        if (isConnected && arduinoPort != null && arduinoPort.IsOpen)
        {
            arduinoPort.Close();
            isConnected = false;
            UpdateConnectionStatus("Disconnected", false);
            Debug.Log("Manually disconnected from Arduino.");
        }
    }
    
    public void ReconnectButton()
    {
        DisconnectButton();
        ConnectButton();
    }
    
    // ===== SLIDER EVENT HANDLERS =====
    
    public void OnTemperatureSliderChanged()
    {
        // Only update if Arduino is not connected
        if (!isConnected)
        {
            temperature = temperatureSlider.value * maxTemperature;
            UpdateUI();
        }
    }
    
    public void OnHumiditySliderChanged()
    {
        // Only update if Arduino is not connected
        if (!isConnected)
        {
            humidity = humiditySlider.value * 100f;
            UpdateUI();
        }
    }
    
    public void OnGasSliderChanged()
    {
        // Only update if Arduino is not connected
        if (!isConnected)
        {
            gasValue = gasSlider.value * maxGasValue;
            UpdateUI();
        }
    }
    
    // ===== DEBUG METHODS =====
    
    public void PrintCurrentValues()
    {
        Debug.Log($"Current Values - Temp: {temperature:F1}°C, Hum: {humidity:F1}%, Gas: {gasValue:F0}");
        Debug.Log($"Last Arduino Data: {lastReceivedData}");
    }
    
    public void SimulateArduinoData()
    {
        // For testing without Arduino
        string testData = $"H:{Random.Range(40f, 95f):F1},T:{Random.Range(20f, 40f):F1},G:{Random.Range(100, 900)}";
        ProcessArduinoData(testData);
        Debug.Log($"Simulated Arduino data: {testData}");
    }
    
    // ===== CLEANUP =====
    
    void OnApplicationQuit()
    {
        CloseSerialPort();
    }
    
    void OnDisable()
    {
        CloseSerialPort();
    }
    
    void CloseSerialPort()
    {
        if (arduinoPort != null)
        {
            if (arduinoPort.IsOpen)
            {
                arduinoPort.Close();
                Debug.Log("Serial port closed.");
            }
            arduinoPort.Dispose();
        }
    }
    
    // ===== GETTER METHODS =====
    
    public float GetTemperature() { return temperature; }
    public float GetHumidity() { return humidity; }
    public float GetGasValue() { return gasValue; }
    public float GetFanSpeed() { return currentFanSpeed; }
    public bool IsArduinoConnected() { return isConnected; }
    public string GetLastReceivedData() { return lastReceivedData; }
}
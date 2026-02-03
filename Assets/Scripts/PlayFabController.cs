using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;
using UnityEngine;

/// <summary>
/// Manages PlayFab integration for user authentication and player statistics.
/// This class follows a singleton pattern to provide a centralized point of access.
/// </summary>
public class PlayFabController : MonoBehaviour
{
    public static PlayFabController PFC;

    private const string EmailKey = "EMAIL";
    private const string PasswordKey = "PASSWORD";

    private string userEmail;                   // User's e-mail address
    private string userPassword;                // User's password
    private string username;                    // User's name
    public GameObject loginPanel;               // Login panel reference

    private void Awake()
    {
        if (PFC == null)
        {
            PFC = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        // Note: Setting title ID here can be skipped if you have set the value in Editor Extensions already.
        if (string.IsNullOrEmpty(PlayFabSettings.staticSettings.TitleId))
        {
            PlayFabSettings.staticSettings.TitleId = "8F582"; // Please change this value to your own titleId from PlayFab Game Manager
        }

        // This is just to login for test purposes
        // var request = new LoginWithCustomIDRequest { CustomId = "GettingStartedGuide", CreateAccount = true };
        // PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);

        // This is used to delete the player's preferences
        // PlayerPrefs.DeleteAll();

        // Autologin is performed if the EmailKey property is on Player Preferences
        if (PlayerPrefs.HasKey (EmailKey))
        {
            userEmail = PlayerPrefs.GetString(EmailKey);
            userPassword = PlayerPrefs.GetString(PasswordKey);

            // To really login, we could use Android or iOS, but we are going to use the player's e-mail
            var request = new LoginWithEmailAddressRequest { Email = userEmail, Password = userPassword };
            PlayFabClientAPI.LoginWithEmailAddress (request, OnLoginSuccess, OnLoginFailure);

            Debug.Log(userEmail + " user logged in automatically.");
        }
        else
        {
            LoginMobile();
        }
    }

    private void LoginMobile()
    {
        #if UNITY_ANDROID
        var requestAndroid = new LoginWithAndroidDevideIDRequest { AndroidDeviceId = ReturnMobileID(), CreateAccount = true };
        PlayFabClientAPI.LoginWithAndroidDeviceID (requestAndroid, OnLoginMobileSuccess, OnLoginMobileFailure);
        #endif
        #if UNITY_IOS
        var requestIOS = new LoginWithIOSDevideIDRequest { DeviceId = ReturnMobileID(), CreateAccount = true };
        PlayFabClientAPI.LoginWithIOSDeviceID (requestIOS, OnLoginMobileSuccess, OnLoginMobileFailure);
        #endif 
    }

    #region Login

    // Method invoked when login is successful
    private void OnLoginSuccess (LoginResult result)
    {
        PlayerPrefs.SetString (EmailKey, userEmail);
        PlayerPrefs.SetString (PasswordKey, userPassword);
        Debug.Log ("Storing " + userEmail + " credentials into Player Preferences.");

        // Disable login panel
        if (loginPanel != null) loginPanel.SetActive(false);

        // Get player statistics
        GetStats();
    }

    // Method invoked when mobile login is successful
    private void OnLoginMobileSuccess (LoginResult result)
    {
        // Disable login panel
        if (loginPanel != null) loginPanel.SetActive(false);

        // Get player statistics
        GetStats();
    }

    // Method invoked when new user registration is successful
    private void OnRegisterSuccess (RegisterPlayFabUserResult result)
    {
        // If user registration was successful, credentials are stored for future autologin
        PlayerPrefs.SetString (EmailKey, userEmail);
        PlayerPrefs.SetString (PasswordKey, userPassword);
        Debug.Log ("Storing " + userEmail + " credentials into Player Preferences.");

        // Disable login panel
        if (loginPanel != null) loginPanel.SetActive(false);

        // Get player statistics
        GetStats();
    }

    // Method invoked when login fails
    private void OnLoginFailure (PlayFabError error)
    {
        // If e-mail login fails, a new account with the provided e-mail address and password is created
        Debug.Log ("User " + userEmail + " does not exist. Registering new player...");

        var registerRequest = new RegisterPlayFabUserRequest { Email = userEmail, Password = userPassword, Username = username };
        PlayFabClientAPI.RegisterPlayFabUser (registerRequest, OnRegisterSuccess, OnRegisterFailure);
    }

    // Method invoked on mobile failure
    private void OnLoginMobileFailure (PlayFabError error)
    {
        Debug.Log (error.GenerateErrorReport());
    }

    // Method invoked on user registration failure
    private void OnRegisterFailure (PlayFabError error)
    {
        Debug.LogError (error.GenerateErrorReport());
    }

    // Sets the user's e-mail address
    public void SetUserEmail (string emailIn)
    {
        userEmail = emailIn;
    }

    // Sets the user's password
    public void SetUserPassword (string passwordIn)
    {
        userPassword = passwordIn;
    }

    // Sets the user's name
    public void SetUsername (string usernameIn)
    {
        username = usernameIn;
    }

    // Invoked when the login button is clicked
    public void OnClickLogin()
    {
        var request = new LoginWithEmailAddressRequest { Email = userEmail, Password = userPassword };
        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnLoginFailure);
    }

    public static string ReturnMobileID()
    {
        string deviceID = SystemInfo.deviceUniqueIdentifier;
        return deviceID;
    }

    #endregion Login

    #region PlayerStats

    public int playerLevel;                 // Player's level statistics
    public int gameLevel;                   // Game's level statistics

    public int playerHealth;                // Player's health statistics
    public int playerDamage;                // Player damage statistics

    public int playerHighScore;             // Player's high score

    // Set player statistics via the client (this method will only work if the "Allow client to post player statistics" is checked
    // on PlayFab Dashboard > Settings > API Features
    public void SetStats()
    {
        PlayFabClientAPI.UpdatePlayerStatistics (new UpdatePlayerStatisticsRequest
        {
            // request.Statistics is a list, so multiple StatisticUpdate objects can be defined if required.
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate { StatisticName = "PlayerLevel", Value = playerLevel },
                new StatisticUpdate { StatisticName = "GameLevel", Value = gameLevel },
                new StatisticUpdate { StatisticName = "PlayerHealth", Value = playerHealth },
                new StatisticUpdate { StatisticName = "PlayerDamage", Value = playerDamage },
                new StatisticUpdate { StatisticName = "PlayerHighScore", Value = playerHighScore }
            }
        },
        result => { Debug.Log ("User statistics updated"); },
        error => { Debug.LogError (error.GenerateErrorReport()); });
    }

    // Method to get player statistics
    public void GetStats()
    {
        PlayFabClientAPI.GetPlayerStatistics (new GetPlayerStatisticsRequest(), OnGetStatistics, error => Debug.LogError(error.GenerateErrorReport()));
    }

    // Method invoked when player statistics are received
    private void OnGetStatistics (GetPlayerStatisticsResult result)
    {
        Debug.Log ("Received the following Statistics:");

        if (result.Statistics == null || result.Statistics.Count == 0)
        {
            Debug.Log("No statistics found for this player. Setting default values...");
            
            // Set default stats for new players
            playerLevel = 1;
            playerHealth = 100;
            playerHighScore = 0;
            gameLevel = 0;
            playerDamage = 0;

            // Update stats via cloud script to initialize them on the server
            StartCloudUpdatePlayerStats();
            return;
        }
        
        // Dictionary to map statistic names to the corresponding fields
        var statsMap = new Dictionary<string, System.Action<int>>
        {
            { "PlayerLevel", (val) => playerLevel = val },
            { "GameLevel", (val) => gameLevel = val },
            { "PlayerHealth", (val) => playerHealth = val },
            { "PlayerDamage", (val) => playerDamage = val },
            { "PlayerHighScore", (val) => playerHighScore = val }
        };

        foreach (var eachStat in result.Statistics)
        {
            Debug.Log ("Statistic (" + eachStat.StatisticName + "): " + eachStat.Value);
            
            if (statsMap.TryGetValue(eachStat.StatisticName, out var assignStat))
            {
                assignStat(eachStat.Value);
            }
        }
    }

    // Update player statistics remotely (via a cloud script)
    public void StartCloudUpdatePlayerStats()
    {
        PlayFabClientAPI.ExecuteCloudScript (new ExecuteCloudScriptRequest()
        {
            FunctionName = "UpdatePlayerStats", // Function name (must exist in your uploaded cloud.js file)
            FunctionParameter = new { Level = playerLevel, highScore = playerHighScore, Health = playerHealth }, // The parameter provided to your function
            GeneratePlayStreamEvent = true, // Optional - Shows this event in PlayStream
        }, OnCloudUpdateStats, OnErrorShared);
    }

    // Invoked when statistics have been updated via a cloud script
    private static void OnCloudUpdateStats (ExecuteCloudScriptResult result)
    {
        // Cloud Script returns arbitrary results, so you have to evaluate them one step and one parameter at a time
        Debug.Log (PluginManager.GetPlugin<ISerializerPlugin> (PluginContract.PlayFab_Serializer).SerializeObject (result.FunctionResult));
        JsonObject jsonResult = (JsonObject) result.FunctionResult;
        object messageValue;
        jsonResult.TryGetValue ("messageValue", out messageValue); // note how "messageValue" directly corresponds to the JSON values set in Cloud Script
        Debug.Log ((string) messageValue);
    }

    private static void OnErrorShared (PlayFabError error)
    {
        Debug.Log (error.GenerateErrorReport());
    }

    #endregion PlayerStats
}
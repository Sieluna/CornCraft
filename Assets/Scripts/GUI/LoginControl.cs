using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

using MinecraftClient.Protocol;
using MinecraftClient.Protocol.Handlers.Forge;
using MinecraftClient.Protocol.Session;

namespace MinecraftClient.UI
{
    public class LoginControl : MonoBehaviour
    {
        private TMP_InputField serverInput, usernameInput, passwordInput;
        private Button         loginButton, quitButton;

        private readonly Dictionary<string, KeyValuePair<string, string>> Accounts = new Dictionary<string, KeyValuePair<string, string>>();
        private readonly Dictionary<string, KeyValuePair<string, ushort>> Servers = new Dictionary<string, KeyValuePair<string, ushort>>();

        private bool tryingConnect = false;

        /// <summary>
        /// Load login/password using an account alias
        /// </summary>
        /// <returns>True if the account was found and loaded</returns>
        public bool SetAccount(string accountAlias)
        {
            accountAlias = accountAlias.ToLower();
            if (usernameInput is not null && passwordInput is not null && Accounts.ContainsKey(accountAlias))
            {
                usernameInput.text = Accounts[accountAlias].Key;
                passwordInput.text = Accounts[accountAlias].Value;
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Load server information in ServerIP and ServerPort variables from a "serverip:port" couple or server alias
        /// </summary>
        /// <returns>True if the server IP was valid and loaded, false otherwise</returns>
        public bool ParseServerIP(string server, out string host, ref ushort port)
        {
            server = server.ToLower();
            string[] sip = server.Split(':');
            host = sip[0];
            port = 25565;

            if (sip.Length > 1)
            {
                try
                {
                    port = Convert.ToUInt16(sip[1]);
                }
                catch (FormatException) { return false; }
            }

            if (host == "localhost" || host.Contains('.'))
            {
                // Server IP (IP or domain names contains at least a dot)
                if (sip.Length == 1 && host.Contains('.') && host.Any(c => char.IsLetter(c)) && CornCraft.ResolveSrvRecords)
                    //Domain name without port may need Minecraft SRV Record lookup
                    ProtocolHandler.MinecraftServiceLookup(ref host, ref port);
                return true;
            }
            else if (Servers.ContainsKey(server))
            {
                // Server Alias (if no dot then treat the server as an alias)
                host = Servers[server].Key;
                port = Servers[server].Value;
                return true;
            }

            return false;
        }

        public void TryConnectServer()
        {
            if (tryingConnect)
                return;
            
            tryingConnect = true;

            ConnectServer();

            tryingConnect = false;
        }

        // Returns true if successfully connected
        public bool ConnectServer()
        {
            string serverText = serverInput.text;
            string account = usernameInput.text;
            string password = passwordInput.text;

            string username; // In-game display name, will be set after connection

            string host;   // Server ip address
            ushort port = 25565; // Server port

            if (!ParseServerIP(serverText, out host, ref port))
            {
                Debug.Log("Failed to parse server name or address!");
                return false;
            }

            bool MinecraftRealmsEnabled = false;

            ProtocolHandler.AccountType AccountType = ProtocolHandler.AccountType.Microsoft;

            SessionToken session = new SessionToken();
            ProtocolHandler.LoginResult result = ProtocolHandler.LoginResult.LoginRequired;

            if (password == "-")
            {   // Enter offline mode
                Translations.Log("mcc.offline");
                result = ProtocolHandler.LoginResult.Success;
                session.PlayerID = "0";
                session.PlayerName = account;
            }
            else
            {   // Validate cached session or login new session.
                if (CornCraft.SessionCaching != CacheType.None && SessionCache.Contains(account.ToLower()))
                {
                    session = SessionCache.Get(account.ToLower());
                    result = ProtocolHandler.GetTokenValidation(session);
                    if (result != ProtocolHandler.LoginResult.Success)
                    {
                        Translations.Log("mcc.session_invalid");
                        // Try to refresh access token
                        if (!string.IsNullOrWhiteSpace(session.RefreshToken))
                        {
                            result = ProtocolHandler.MicrosoftLoginRefresh(session.RefreshToken, out session, ref account);
                        }
                        if (result != ProtocolHandler.LoginResult.Success && password == string.Empty)
                        {   // Request password
                            Debug.LogWarning("Please input your password!");
                            return false;
                        }
                    }
                    else
                        Translations.Log("mcc.session_valid", session.PlayerName);
                }

                if (result != ProtocolHandler.LoginResult.Success)
                {
                    Translations.Log("mcc.connecting", AccountType == ProtocolHandler.AccountType.Mojang ? "Minecraft.net" : "Microsoft");
                    result = ProtocolHandler.GetLogin(account, password, AccountType, out session, ref account);
                }
            }

            if (result == ProtocolHandler.LoginResult.Success && CornCraft.SessionCaching != CacheType.None)
            {
                SessionCache.Store(account.ToLower(), session);
            }

            if (result == ProtocolHandler.LoginResult.Success)
            {
                // Update the in-game user name
                username = session.PlayerName;
                bool isRealms = false;

                if (CornCraft.DebugMode)
                    Translations.Log("debug.session_id", session.ID);

                List<string> availableWorlds = new List<string>();
                if (MinecraftRealmsEnabled && !String.IsNullOrEmpty(session.ID))
                    availableWorlds = ProtocolHandler.RealmsListWorlds(username, session.PlayerID, session.ID);

                if (host == "")
                    return false;

                // Get server version
                int protocolVersion = 0;
                ForgeInfo forgeInfo = null;

                if (!isRealms)
                {
                    Translations.Log("mcc.retrieve");
                    // Retrieve server information
                    if (!ProtocolHandler.GetServerInfo(host, port, ref protocolVersion, ref forgeInfo))
                    {
                        Translations.LogError("error.ping");
                        return false;
                    }
                }

                // Proceed to server login
                if (protocolVersion != 0)
                {
                    try
                    {
                        CornClient.StartClient(session.PlayerName, session.PlayerID, session.ID, host, port, protocolVersion, forgeInfo);
                        return true;
                    }
                    catch (NotSupportedException)
                    {
                        Translations.LogError("error.unsupported");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Unexpected error: " + e.StackTrace);
                    }
                }
                else // Unable to determine server version
                    Translations.LogError("error.determine");
            }
            else
            {
                string failureMessage = Translations.Get("error.login");
                string failureReason = "";
                switch (result)
                {
                    case ProtocolHandler.LoginResult.AccountMigrated: failureReason = "error.login.migrated"; break;
                    case ProtocolHandler.LoginResult.ServiceUnavailable: failureReason = "error.login.server"; break;
                    case ProtocolHandler.LoginResult.WrongPassword: failureReason = "error.login.blocked"; break;
                    case ProtocolHandler.LoginResult.InvalidResponse: failureReason = "error.login.response"; break;
                    case ProtocolHandler.LoginResult.NotPremium: failureReason = "error.login.premium"; break;
                    case ProtocolHandler.LoginResult.OtherError: failureReason = "error.login.network"; break;
                    case ProtocolHandler.LoginResult.SSLError: failureReason = "error.login.ssl"; break;
                    case ProtocolHandler.LoginResult.UserCancel: failureReason = "error.login.cancel"; break;
                    default: failureReason = "error.login.unknown"; break;
                }
                failureMessage += Translations.Get(failureReason);

                if (result == ProtocolHandler.LoginResult.SSLError)
                {
                    Translations.LogError("error.login.ssl_help");
                    return false;
                }
                Debug.LogError(failureMessage);
            }

            return false;
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        void Start()
        {
            // Initialize controls
            var loginPanel = transform.Find("Login Panel");

            serverInput   = loginPanel.transform.Find("Server Input").GetComponent<TMP_InputField>();
            usernameInput = loginPanel.transform.Find("Username Input").GetComponent<TMP_InputField>();
            passwordInput = loginPanel.transform.Find("Password Input").GetComponent<TMP_InputField>();
            loginButton   = loginPanel.transform.Find("Login Button").GetComponent<Button>();

            quitButton    = transform.Find("Quit Button").GetComponent<Button>();

            // TODO Initialize with loaded values
            //serverInput.text = "192.168.1.2";
            serverInput.text = "192.168.1.38";
            usernameInput.text = "Corn";
            passwordInput.text = "-";

            // Add listeners
            loginButton.onClick.AddListener(this.TryConnectServer);
            quitButton.onClick.AddListener(this.QuitGame);

        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                CornClient.ShowNotification("Test Message", 4F, Notification.Type.Notification);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                CornClient.ShowNotification("Error Message", 4F, Notification.Type.Error);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                CornClient.ShowNotification("Warning Message", 4F, Notification.Type.Warning);
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                CornClient.ShowNotification("Success Message", 4F, Notification.Type.Success);
            }

        }

    }
}

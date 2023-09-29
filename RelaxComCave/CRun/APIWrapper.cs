using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace RelaxComCave.Runner {

    public enum MessageTypes {
        None = 0,
        Warning = 1,
        Error = 2,
        OK = 3
    }

    public enum StandortType {
        error = -1,
        Comcave = 2,
        Home = 3
    }

    public sealed class APIWrapper : CCCon {

        #region Variabeln
        private const string MainPage = "https://portal.cc-student.com/index.php";
        private const string LoginPage = "https://portal.cc-student.com/index.php?cmd=login";
        private const string KuGPage = "https://portal.cc-student.com/index.php?cmd=kug";
        private const string LogoutPage = "https://portal.cc-student.com/index.php?cmd=logout";
        private const string StandortMessageHome = "Sie befinden sich aktuell nicht an einem Comcave Standort.";

        private string LoginName = string.Empty;
        private string Kennwort = string.Empty;

        private Action<string, int>? MessageCallback = null;
        #endregion

        #region Properties

        /// <summary>
        /// Gibt einen Wert zurück, der anzeigt ob der Nutzer eingelogt ist.
        /// </summary>
        public bool IsLoggedIn { get; private set; }
        /// <summary>
        /// Gibt den Namen des angemeldeten Nutzers zurück.
        /// </summary>
        public string PortalName { get; private set; }
        /// <summary>
        /// Gibt den zuletzt korrekten Login zurück
        /// </summary>
        public string CurrentLogin { get => this.LoginName; }
        #endregion

        #region Konstruktor
        public APIWrapper() : base() { PortalName = string.Empty; }
        public APIWrapper(Action<string, int> messageCallback) : base() { this.MessageCallback = messageCallback; PortalName = string.Empty; }
        #endregion

        #region Login
        /// <summary>
        /// Führt einen Login mit den zuletzt verwendeten Logindaten aus.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TryLogin() {
            if(!string.IsNullOrEmpty(LoginName) && !string.IsNullOrEmpty(Kennwort)) {
                return await TryLogin(LoginName, Kennwort);
            }
            return false;
        }
        /// <summary>
        /// Versucht einen Login
        /// </summary>
        /// <param name="username">Username zum einloggen</param>
        /// <param name="password">Kennwort</param>
        /// <returns></returns>
        public async Task<bool> TryLogin(string username, string password) {

            //Nach dem aktuellen Session Cookie suchen, falls nötig eines erstellen.
            if (!Headers.Values.Any(x => x.Contains("ccportalsessid"))) {
                if (!await TryGetSessionCookie()) {
                    if (MessageCallback != null) MessageCallback("Kein SessionToken gefunden. Login abgebrochen!", (int)MessageTypes.Error);
                    return false;
                }
            }

            //Nun benötigen wir die ChallengeResponse

            var loginPage = await GET_Request(LoginPage);
            if (loginPage == null) { if (MessageCallback != null) MessageCallback("Die Loginseite konnte nicht geladen werden.", (int)MessageTypes.Error); return false; }

            //In der LoginPage müsste nun ein Element mit dem Aufbau: <input type="hidden" name="challengeResponse" value="f64fa0e7d2" /> befinden. Wir benötigen den Wert.

            string MainContent = await loginPage.Content.ReadAsStringAsync();

            var challengeResponse = TryToGetChallenge(MainContent);
            if (challengeResponse == null) { if (MessageCallback != null) MessageCallback("Die ChallengeResponse wurde nicht gefunden.", (int)MessageTypes.Error); return false; }

            //Für den Request muss nun der String erstellt werden. Dieser besteht aus folgender Zeichenfolge ->   username:PasswortObfuscated:ChallengeResponse
            var cryptKennwort = MatCrypt(password);
            var toCrypt = $"{username.ToLower()}:{cryptKennwort}:{challengeResponse}";
            var finCryptStr = CreateMD5(toCrypt)?.ToLower();

            //Nun befindet sich der "Verschlüsselte" Part bereit zum übertragen.
            //Wir basteln den Post Request Content zusammen
            var post = $"challengeResponse={challengeResponse}&pwEnc={finCryptStr}&login_username={username}&login_passwort=&login_submit=Einloggen";

            var postResponse = await POST_Request(LoginPage, post);
            if (postResponse == null) { if (MessageCallback != null) MessageCallback("Die PostResponse war Null", (int)MessageTypes.Error); return false; }

            //Wir überprüfen ob in der Response eine gültiger Login verzeichnet wurde.
            string loginResult = await postResponse.Content.ReadAsStringAsync();

            //Wir speichern ob der Benutzer eingeloggt wurde.
            this.IsLoggedIn = TryToFindLogInState(loginResult, out string PortalBenutzer);

            loginPage.Dispose();
            postResponse.Dispose();
            if (!IsLoggedIn) {
                if (MessageCallback != null) MessageCallback("Der Login war nicht erfolgreich!", (int)MessageTypes.Error);
                return false;
            } else {
                LoginName = username; Kennwort = password;
                if (MessageCallback != null) {
#if DEBUG
                    this.PortalName = "Mustermann";
#else
this.PortalName = PortalBenutzer;
#endif
                    MessageCallback("Der Login war erfolgreich!", (int)MessageTypes.OK);
                    MessageCallback($"Der User {PortalBenutzer} wurde angemeldet.", (int)MessageTypes.OK);
                }
                return true;
            }
        }

#endregion

        #region Logout
        /// <summary>
        /// Führt den Logout aus
        /// </summary>
        /// <returns></returns>
        public async Task<bool> LogOut() {
            if (IsLoggedIn) {
                IsLoggedIn = false;
                try {
                    using(var response = await GET_Request(LogoutPage)) {
                        this.Headers.Clear();
                        if(MessageCallback != null)MessageCallback($"Der User {this.PortalName} logoff.", (int)MessageTypes.OK);
                        return true;
                    }
                }catch(Exception) { if (MessageCallback != null) MessageCallback($"Der User {this.PortalName} konnte nicht abgemeldet werden.", (int)MessageTypes.Warning); return false; }
            } else { if (MessageCallback != null) MessageCallback($"Der User {this.PortalName} ist nicht eingeloggt!", (int)MessageTypes.Warning); return true; }
        }

        #endregion

        #region Standort
        /// <summary>
        /// Überprüft ob der Teilnehmer am Standort eingelogt ist.
        /// </summary>
        /// <returns></returns>
        public async Task<StandortType> ComCaveStandort(bool useCallback = true) {
            if (!IsLoggedIn) return StandortType.error;
            try {
                var zeitresponse = await GET_Request(KuGPage);
                if (zeitresponse == null) return StandortType.error;
                var message = await zeitresponse.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(message)) return StandortType.error;

                //Nun müssen wir den IFrame inhalt besorgen.
                string teile = message.Remove(0, message.IndexOf("<iframe id=\"TheFrame\" src=\"") + 27);
                teile = teile.Substring(0, teile.IndexOf("\""));

                var restResponse = await GET_Request(teile);
                if (restResponse == null) return StandortType.error;

                message = await restResponse.Content.ReadAsStringAsync();

                restResponse.Dispose();
                zeitresponse.Dispose();

                if (message.Contains(StandortMessageHome)) {
                    if (MessageCallback != null && useCallback) MessageCallback("Standort wurde ermittelt.", (int)MessageTypes.None);
                    return StandortType.Home;
                } else {
                    if (MessageCallback != null && useCallback) MessageCallback("Standort wurde ermittelt.", (int)MessageTypes.None);
                    return StandortType.Comcave;
                }
            } catch (Exception e) {
                return StandortType.error;
            }
        }
        #endregion

        #region SessionToken
        /// <summary>
        /// Sucht anhand String Contents ob der User angemeldet ist.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private bool TryToFindLogInState(string content, out string UserName) {
            try {
                if (content.Contains("Nicht angemeldet")) {
                    UserName = string.Empty;
                    return false;
                } else {
                    UserName = string.Empty;
                    if (content.Contains("Willkommen in Ihrem CC-Portal, ")) {
                        int start = content.IndexOf("Willkommen in Ihrem CC-Portal, ") + 31;
                        for (int i = start; i < start + 25; i++) {
                            if (content[i] == '.') {
                                break;
                            } else {
                                UserName += content[i];
                            }
                        }
                    }
                    return true;
                }
            } catch (Exception) { UserName = string.Empty; return false; }
        }

        /// <summary>
        /// Versucht ein aktuelles Session Token zu bekommen
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TryGetSessionCookie() {
            try {
                using (var mainResponse = await GET_Request(MainPage)) {
                    if (mainResponse == null) return false;
                    var sessionHolders = mainResponse.Headers.FirstOrDefault(x => x.Key == "Set-Cookie");
                    if (!sessionHolders.Value.Any()) return false;
                    var splitCookies = sessionHolders.Value.First().Split(';');
                    var sessionCookie = splitCookies.FirstOrDefault(x => x.Contains("ccportalsessid"));
                    if (sessionCookie == null) return false;
                    var sessionSplit = sessionCookie.Split("=");
                    if (sessionSplit.Length < 2) return false;
                    if (!base.Headers.ContainsKey("Cookie")) {
                        base.Headers.Add("Cookie", $"ccportalsessid={sessionSplit[1]}");
                    }
                    return true;
                }
            } catch (Exception) { return false; }
        }
        #endregion

        #region SendKommenGehen

        /// <summary>
        /// Sendet einen Kommen Post an den ComCave Server
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SubmitKommen() {
            if (!IsLoggedIn) return false;
            //Zuerst benötigen wir wieder den Submit Teil.
            try {
                string PostMessage = string.Empty;

                using (var zeitPage = await GET_Request(KuGPage)) {
                    if (zeitPage == null) return false;
                    var responseZeitPage = await zeitPage.Content.ReadAsStringAsync();

                    if (string.IsNullOrEmpty(responseZeitPage) && !responseZeitPage.Contains("kommengehenformContainer")) return false;

                    //Überprüfen, ob der Nutzer nicht bereits als Kommen gebucht ist!
                    if (responseZeitPage.Contains("value=\"Gehen\"")) {
                        //Der Nutzer ist bereits als kommen gebucht!
                        if (MessageCallback != null) MessageCallback("SubmitKommen wurde abgebrochen. Der Nutzer ist bereits als Anwesend gebucht!", (int)MessageTypes.Warning); return true;
                    }

                    string teilPartg = responseZeitPage.Remove(0, responseZeitPage.IndexOf("kommengehenformContainer"));
                    string? ActionCode = TryToGetKommenGehenAction(teilPartg);
                    if (ActionCode == null) return false;
                    PostMessage = $"action={ActionCode}&kommengehenbutton=Kommen";
                }

                using (var postResult = await POST_Request(KuGPage, PostMessage)) {
                    if (postResult == null) return false;
                    if (postResult.StatusCode == System.Net.HttpStatusCode.OK) {
                        if (MessageCallback != null) MessageCallback("Submit Kommen -> Response[200:OK]", (int)MessageTypes.None);
                        return true;
                    } else {
                        if (MessageCallback != null) MessageCallback($"Submit Kommen -> Response[{(int)postResult.StatusCode}:{postResult.StatusCode.ToString()}]", (int)MessageTypes.Warning);
                        return false;
                    }
                }
            } catch(Exception) { return false; }        
        }

        /// <summary>
        /// Sendet einen Gehen Post an den ComCave Server
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SubmitGehen() {
            if (!IsLoggedIn) return false;
            //Zuerst benötigen wir wieder den Submit Teil.
            try {
                string PostMessage = string.Empty;

                using (var zeitPage = await GET_Request(KuGPage)) {
                    if (zeitPage == null) return false;
                    var responseZeitPage = await zeitPage.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(responseZeitPage) && !responseZeitPage.Contains("kommengehenformContainer")) return false;

                    //Überprüfen, ob der Nutzer nicht bereits als abwesend gebucht ist!
                    if (responseZeitPage.Contains("value=\"Kommen\"")) {
                        //Der Nutzer ist bereits als kommen gebucht!
                        if (MessageCallback != null) MessageCallback("SubmitKommen wurde abgebrochen. Der Nutzer ist bereits als Abwesend gebucht!", (int)MessageTypes.Warning); return true;
                    }

                    string teilPartg = responseZeitPage.Remove(0, responseZeitPage.IndexOf("kommengehenformContainer"));
                    string? ActionCode = TryToGetKommenGehenAction(teilPartg);
                    if (ActionCode == null) return false;
                    PostMessage = $"action={ActionCode}&kommengehenbutton=Gehen";
                }

                using (var postResult = await POST_Request(KuGPage, PostMessage)) {
                    if (postResult == null) return false;
                    if (postResult.StatusCode == System.Net.HttpStatusCode.OK) {
                        if (MessageCallback != null) MessageCallback("Submit Gehen -> Response[200:OK]", (int)MessageTypes.None);
                        return true;
                    } else {
                        if (MessageCallback != null) MessageCallback($"Submit Gehen -> Response[{(int)postResult.StatusCode}:{postResult.StatusCode.ToString()}]", (int)MessageTypes.Warning);
                        return false;
                    }
                }
            } catch (Exception) { return false; }
        }

        #endregion

        #region IsErfasst
        public async Task<bool> IsErfasst() {
            if (!IsLoggedIn) return false;
            //Zuerst benötigen wir wieder den Submit Teil.
            try {
                string PostMessage = string.Empty;

                using (var zeitPage = await GET_Request(KuGPage)) {
                    if (zeitPage == null) return false;
                    var responseZeitPage = await zeitPage.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(responseZeitPage) && !responseZeitPage.Contains("kommengehenformContainer")) return false;

                    //Überprüfen, ob der Nutzer nicht bereits als abwesend gebucht ist!
                    if (responseZeitPage.Contains("value=\"Kommen\"")) {
                        //Der Nutzer ist bereits als kommen gebucht!
                        return false;
                    }else { return true; }
                }
            } catch (Exception) { return false; }
        }
        #endregion

        #region Tools
        /// <summary>
        /// Berechnet eine MD5 Checksumme
        /// </summary>
        /// <param name="RawInput"></param>
        /// <returns></returns>
        private string? CreateMD5(string RawInput) {
            try {
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create()) {
                    byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(RawInput);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    return Convert.ToHexString(hashBytes);
                }
            } catch (Exception) { return null; }
        }

        /// <summary>
        /// Versucht den Challenge Code zu parsen
        /// </summary>
        /// <param name="RawHtml">Raw HTML Content</param>
        /// <returns></returns>
        private string? TryToGetChallenge(string RawHtml) {
            try {
                string pattern = @"<input type=""hidden"" name=""challengeResponse"" value=""(.*?)"" />";
                Regex regex = new Regex(pattern, RegexOptions.Compiled);
                var match = regex.Match(RawHtml);
                if (match.Success && match.Groups.Count >= 2) {
                    return match.Groups[1].Value;
                } else { return null; }
            } catch (Exception) { return null; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="RawHtml"></param>
        /// <returns></returns>
        private string? TryToGetKommenGehenAction(string RawHtml) {
            try {
                string pattern = @"<input type=""hidden"" name=""action"" value=""(.*?)"" />";
                Regex regex = new Regex(pattern, RegexOptions.Compiled);
                var match = regex.Match(RawHtml);
                if (match.Success && match.Groups.Count >= 2) {
                    return match.Groups[1].Value;
                } else { return null; }
            } catch (Exception) { return null; }
        }

        /// <summary>
        /// Diese Funktion scheint eine ComCave eigene Creation zum verschleiern des Kennwortes zu sein.
        /// Originale Funktion -> cached_20230721_62bcdd1dd6.js -> LINE 804.
        /// Die Erste der Funktion ist MathCrypt, die nächste im JS stellt den Hash Algo dar.
        /// Perform Login erstellt am Ende die Values welche im POST Request verwendet werden.
        /// </summary>
        /// <param name="RawKennwort">Das Originale Kennwort</param>
        /// <returns></returns>
        private string? MatCrypt(string RawKennwort) {
            string crypt_key = "ID";
            int len = 0;
            int expand = 15;
            string passw = "";
            int i = 0;
            if (RawKennwort.Length == 0)
                return "";
            do {
                i = 0;
                passw += RawKennwort;
            } while (passw.Length < expand);
            passw = passw.Substring(0, expand);
            len = passw.Length;
            char[] passwChars = passw.ToCharArray();
            for (i = 0; i < passwChars.Length; i++) {
                passwChars[i] = (char)passwChars[i];
            }
            if (len > expand || len > 255)
                len = expand;
            int a = passwChars[len - 1] % len;
            int b = passwChars[0] % len;
            for (i = 0; i < len; i++) {
                a = (a + 7) % len;
                b = (a + 11) % len;
                passwChars[a] = (char)(passwChars[a] ^ passwChars[b]);
                passwChars[b] = (char)(passwChars[a] ^ passwChars[b]);
                passwChars[a] = (char)(passwChars[b] ^ passwChars[a]);
            }
            int i_key = 0;
            if (crypt_key != "PW") {
                for (i = 0; i < crypt_key.Length; ++i) {
                    i_key += crypt_key[i] - '0';
                }
                i_key %= 100;
            }
            for (i = 0; i < len; ++i) {
                a = 0;
                int sum = 0;
                for (int ax = 0; ax < len; ++ax) {
                    sum += passwChars[ax];
                }
                sum = (passwChars[i] + sum) % 128 + i_key;

                if (sum < 47) {
                    passwChars[i] = (char)(sum % 10 + 48);
                } else if (sum > 87) {
                    passwChars[i] = (char)(sum % 26 + 65);
                } else {
                    passwChars[i] = (char)(sum % 26 + 97);
                }
            }
            return new string(passwChars);
        }
        #endregion

    }


}



using RelaxComCave.Runner;
using RelaxComCave.Tools;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace RelaxComCave {
    internal class Program {

        #region DLL Import
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        #endregion

        #region Top
        /// <summary>
        /// Enthält alle angemeldeteten Users.
        /// </summary>
        private static List<APIWrapper> MyUsers = new List<APIWrapper>();

        /// <summary>
        /// Token zum Canceln des Shutdownmanagers
        /// </summary>
        private static CancellationTokenSource? token = null;
        /// <summary>
        /// Zeit zu der Nutzer und PC Actionen ausgeführt werden
        /// </summary>
        private static DateTime? ShutdownTime = null;
        /// <summary>
        /// Bestimmt ob die Nutzer abgemeldet werden sollen.
        /// </summary>
        private static bool SubmitGehenIfShutdownRequest = true;
        /// <summary>
        /// Bestimmt ob der PC heruntergefahren werden soll.
        /// </summary>
        private static bool ShutdownPCIfTimeOver = false;

        #endregion

        #region MainEntry
        static async Task Main(string[] args) {
            Console.Title = "RelaxComCave Online Launcher 1.2";
            await MainRoutine();
        }
        #endregion

        #region MainMenu
        private static async Task MainRoutine() {
            var AscIIBall = GetASCIIFromManifest().Split('\n');
            while (true) {
                Console.CursorVisible = true;
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.SetCursorPosition(0, 1);
                var bigLength = AscIIBall.Max(x => x.Length);

                int start = (Console.BufferWidth / 2) - (bigLength / 2);

                int yy = 1;
                foreach (var bPart in AscIIBall) {
                    Console.SetCursorPosition(start, yy);
                    Console.Write(bPart.ToString());
                    yy++;
                }

                string TitleP1 = "RelaxComCave Online Launcher 1.2";
                string TitleP2 = "By Farliam 19.09.2023";

                int titleP1Begin = (Console.BufferWidth / 2) - TitleP1.Length / 2;
                int titleP2Begin = (Console.BufferWidth / 2) - TitleP2.Length / 2;

                Console.SetCursorPosition(titleP1Begin, yy + 2);
                Console.Write(TitleP1);
                Console.SetCursorPosition(titleP2Begin, yy + 3);
                Console.WriteLine(TitleP2);
                Console.WriteLine();
                for (int i = 0; i < Console.BufferWidth; i++) {
                    Console.Write("*");
                }

                if (MyUsers.Any()) {
                    Console.WriteLine($"\nDerzeit angemeldet: {MyUsers.Count}");
                }

                if(token != null){
                    Console.WriteLine("ShutdownManager Running!");
                }

                Console.WriteLine();

                Console.WriteLine("Hauptmenü:");
                Console.WriteLine("1. User Login");
                Console.WriteLine("2. User Manager");
                Console.WriteLine("3. Herunterfahren planen");
                Console.WriteLine("4. Verstecken");
                Console.WriteLine("5. Exit");
                Console.WriteLine();
                Console.Write("Eingabe: ");
                await ParseAuswahl();
            }
        }

        private static async Task ParseAuswahl() {
            string? inputMessage = Console.ReadLine();
            if (inputMessage != null) {
                if (int.TryParse(inputMessage, out int value)) {
                    switch (value) {
                        case 1:
                            await CreateLogin();
                            break;
                        case 2:
                            await UserManager();
                            break;
                        case 3:
                            ShutdownManager();
                            break;
                        case 4:
                            HideMe();
                            break;
                        case 5:
                            Environment.Exit(0);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private static async Task UserManager() {
            bool isRunning = true;
            while (isRunning) {

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Angemeldete Benutzer:");

                int i = 1;

                foreach (var user in MyUsers) {
                    string UserName = user.PortalName.PadRight(25);
                    Console.Write($"{i}. {UserName}");
                    Console.Write("\tErfassung: ");
                    if (await user.IsErfasst()) {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Aktiv");
                    } else {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Nicht aktiv");
                    }
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Green;
                    var standort = await user.ComCaveStandort(false);
                    if (standort == StandortType.Comcave) {
                        Console.WriteLine("\tStandort: Comcave");
                    } else if (standort == StandortType.Home) {
                        Console.WriteLine("\tStandort: Home");
                    } else {
                        Console.WriteLine("\tStandort: Error");
                    }
                    i++;
                }

                Console.WriteLine("\nOptionen\n1. Alle Kommen\n2. Alle Gehen\n3. Alle Abmelden\n4. Alle Anmelden\n5. Benutzer\n6. Hauptmenü");
                Console.Write("\nEingabe: ");

                string? inputMessage = Console.ReadLine();
                if (inputMessage != null) {
                    if (int.TryParse(inputMessage, out int value)) {
                        switch (value) {
                            case 1:
                                Console.Clear();
                                foreach(var usr in MyUsers) {
                                    Message("User: " + usr.PortalName, (int)MessageTypes.None);
                                    var res = await usr.SubmitKommen();
                                }
                                Console.WriteLine("Drücke eine Taste..."); Console.ReadKey();
                                break;
                            case 2:
                                Console.Clear();
                                foreach (var usr in MyUsers) {
                                    Message("User: " + usr.PortalName, (int)MessageTypes.None);
                                    var res = await usr.SubmitGehen();
                                }
                                Console.WriteLine("Drücke eine Taste..."); Console.ReadKey();
                                break;
                            case 3:
                                Console.Clear();
                                foreach (var usr in MyUsers) {
                                    Message("User: " + usr.PortalName, (int)MessageTypes.None);
                                    var res = await usr.LogOut();
                                }
                                Console.WriteLine("Drücke eine Taste..."); Console.ReadKey();
                                break;
                            case 4:
                                Console.Clear();
                                foreach (var usr in MyUsers) {
                                    Message("User: " + usr.PortalName, (int)MessageTypes.None);
                                    var res = await usr.TryLogin();
                                }
                                Console.WriteLine("Drücke eine Taste..."); Console.ReadKey();
                                break;
                            case 5:
                                Console.Write("Nutzernummer: ");
                                var nutznumma = Console.ReadLine();
                                await NutzerOperation(nutznumma ?? "");
                                break;
                            case 6:
                                isRunning = false;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private static async Task NutzerOperation(string input) {
            bool isRunning = true;
            while (isRunning) {
                if (input != null) {
                    if (int.TryParse(input, out int value)) {
                        if(value > 0 && value <= MyUsers.Count) {
                            var usr = MyUsers[value -1];
                            Console.Clear();
                            Console.WriteLine(usr.PortalName);
                            Console.WriteLine("1. Nutzer Kommen\n2. Nutzer gehen\n3. Exit");
                            Console.Write("Eingabe: ");
                            var nInput = Console.ReadLine(); 
                            if(int.TryParse(nInput,out int nValue)) {
                                if(nValue == 1) {
                                   await usr.SubmitKommen();
                                    Console.WriteLine("Drücke eine Taste...");Console.ReadKey();
                                }else if(nValue == 2) {
                                    await usr.SubmitGehen();
                                    Console.WriteLine("Drücke eine Taste..."); Console.ReadKey();
                                } else if(nValue == 3) {
                                    isRunning = false;
                                }
                            }                                                          
                        }
                    }
                }
            }
        }

        private static async Task CreateLogin() {
            Console.Clear();
            Console.Write("Loginname: ");
            string? Login = Console.ReadLine();
            Console.Write("Kennwort: ");
            string? kennwort = Console.ReadLine();

            if (MyUsers.Any(x => x.CurrentLogin == Login)) {
                Message("Der Nutzer ist bereits angemeldet!", (int)MessageTypes.Warning);
                Console.WriteLine("Drücke eine Taste zum fortfahren...");
                Console.ReadKey();
                return;
            }

            Console.Clear();
            Message("Try Login...", (int)MessageTypes.None);
            var NewUser = new APIWrapper(Message);
            if (string.IsNullOrEmpty(Login) | string.IsNullOrEmpty(kennwort)) return;
            var LoginResult = await NewUser.TryLogin(Login ?? string.Empty, kennwort ?? string.Empty);
            if (LoginResult) {
                MyUsers.Add(NewUser);
                Message("Benutzer wurde hinzugefügt", (int)MessageTypes.OK);
            } else {
                Message("Der Benutzer wurde nicht hinzugefügt.", (int)MessageTypes.OK);
            }
            Console.WriteLine("Drücke eine Taste zum fortfahren...");
            Console.ReadKey();
        }

        #endregion

        #region Tools
        public static void Message(string message, int type) {
            var messageType = (MessageTypes)type;
            switch (messageType) {
                case MessageTypes.None:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case MessageTypes.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case MessageTypes.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case MessageTypes.OK:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                default:
                    break;
            }
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")}: {message}");
            Console.ResetColor();
        }

        public static string GetASCIIFromManifest() {
            try {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RelaxComCave.InsertASCII.txt")) {
                    if (stream == null) return string.Empty;
                    using (var reader = new StreamReader(stream)) {
                        var sb = new StringBuilder();
                        sb.Append(reader.ReadToEnd());
                        return sb.ToString();
                    }
                }
            } catch (Exception ex) {
                Message(ex.Message, (int)MessageTypes.Error);
                return string.Empty;
            }
        }

        private static bool ParseOp(string input) {
            var tl = input.ToLower();
            return tl == "yes" | tl == "ja" | tl == "1" | tl == "y" | tl == "j";
        }
        #endregion

        #region ShutdownManager
        private static void ShutdownManager() {
            if (token != null) {
                Message("Ein Timer läuft bereits! Timer wurde gestoppt!", (int)MessageTypes.Warning);
                Message("Drücke eine Taste...", (int)MessageTypes.Warning);
                Console.ReadKey();
                token.Cancel();
                token = null;
                return;
            }
            while (true) {
                Console.Clear();
                Console.Write("Bitte Startzeit eingeben: ");
                string? myTime = Console.ReadLine();
                Console.Write("Sollen die Nutzer abgemeldet werden? (ja,nein): ");
                string? AbmeldenText = Console.ReadLine();
                Console.Write("Soll der PC heruntergefahren werden? (ja,nein): ");
                string? shutdownText = Console.ReadLine();

                if (!DateTime.TryParse(myTime, out DateTime fixTime)) {
                    Message("Die Zeit konnte nicht eingelesen werden!", (int)MessageTypes.Error);
                    Console.WriteLine("Drücke eine Taste...");
                    Console.ReadKey(); return;
                }

                bool shutdown = ParseOp(shutdownText ?? "0");
                bool abmelden = ParseOp(AbmeldenText ?? "0");


                for (int i = 0; i < Console.BufferWidth; i++) {
                    Console.Write("*");
                }

                Console.WriteLine($"Daten:\nZeitpunkt: {fixTime.ToString("HH:mm:ss")}\nShutdown: {shutdown.ToString()}\nAbmelden: {abmelden.ToString()}");

                Console.WriteLine("Sollen die Daten übernommen werden? (ja,nein)");
                string? okText = Console.ReadLine();

                if(ParseOp(okText ?? "0")) {
                    SubmitGehenIfShutdownRequest = abmelden;
                    ShutdownPCIfTimeOver = shutdown;
                    ShutdownTime = fixTime;
                    token = new CancellationTokenSource();
                    var run = Task.Run(async () => await ShutdownRunner());
                    break;
                } else {
                    break;
                }
            }         
        }
        private static async Task ShutdownRunner() {
            if(token == null) return;
            if(ShutdownTime == null) return;
            while (token != null && !token.IsCancellationRequested) {
                if((ShutdownTime.Value - DateTime.Now).TotalSeconds <= 0) {
                    if (SubmitGehenIfShutdownRequest) {
                        foreach(var usr in MyUsers) {
                            usr.ClearSession();        // Aktuell scheint es hier Probleme zu geben. Das Session Token verliert nach 30 Minuten (?) die Gültigkeit 
                            await usr.TryLogin();      // Deswegen die vorherige Session löschen, einen neuen Login erstellen.
                            await usr.SubmitGehen();   // Anschließend ausloggen. Testen....
                        }
                    }
                    if (ShutdownPCIfTimeOver) {
                        Process.Start("shutdown", "/s /f /t 0");
                    } else {
                        break;
                    }
                }
                Thread.Sleep(1000);
            }
            token = null;
        }

        #endregion

        #region Verstecken

        private static void HideMe() {

            string Daten = string.Empty;
            string DatenRe = string.Empty;

            while (true) {
                Console.Clear();
                Console.Write("Text zum Anzeigen eingeben: ");
                Daten = Console.ReadLine() ?? "";
                Console.Clear();
                Console.Write("Bitte erneut eingeben: ");
                DatenRe = Console.ReadLine() ?? "";
                if (Daten == DatenRe) {
                    break;
                }
            }

            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            while (true) {
                string input = ClipboardWatcher.GetClipboardData();
                if(input == Daten) {
                    break;
                }
                Thread.Sleep(1000);
            }

            ShowWindow(handle, SW_SHOW);
        }

        #endregion

    }
}

using System;
using System.Collections.Generic;
using System.Threading;

namespace Sentinel
{
    // ================= MAIN PROGRAM =================
    class Program
    {
        // [REQUIREMENT: COLLECTIONS]
        static List<Country> worldCountries = new List<Country>();
        static Player mainPlayer;

        static bool isGameRunning = true;
        static double gameHour = 0;

        static int defconLevel = 3;
        static GameLevel currentLevel = new Defcon3Level(); // [REQUIREMENT: GAME LEVELS AS OBJECTS]

        static string inputCommand = "";
        static DateTime lastTick = DateTime.Now;
        static DateTime lastNewsTick = DateTime.Now.AddSeconds(-7);
        static Random rng = new Random();
        static List<string> newsFeed = new List<string>();
        static List<string> eventLog = new List<string>();

        static void Main()
        {
            SetupConsole();
            InitializeGameObjects();

            mainPlayer = SelectCountryScreen();

            // Assign exactly 2 allies
            while (mainPlayer.GetCountry().GetAllyCount() < 2)
            {
                Country randomAlly = worldCountries[rng.Next(worldCountries.Count)];

                if (randomAlly.GetName() != mainPlayer.GetCountry().GetName() && !mainPlayer.GetCountry().HasAlly(randomAlly.GetName()))
                {
                    mainPlayer.GetCountry().AddAlly(randomAlly.GetName());
                    randomAlly.AddAlly(mainPlayer.GetCountry().GetName());
                }
            }

            Console.Clear();

            // The Main Game Loop
            while (isGameRunning)
            {
                ReadPlayerInput();

                if ((DateTime.Now - lastTick).TotalMilliseconds > 500)
                {
                    gameHour += (1.75 / 60.0);

                    // --- LEVEL PROGRESSION LOGIC ---
                    if (gameHour >= 8.0 && currentLevel is Defcon3Level)
                    {
                        currentLevel = new Defcon2Level();
                        defconLevel = 2;
                        LogEvent(">>> DEFCON 2 DECLARED: MOBILIZATION FORCES ENGAGED <<<");
                    }
                    else if (gameHour >= 16.0 && currentLevel is Defcon2Level)
                    {
                        currentLevel = new Defcon1Level();
                        defconLevel = 1;
                        LogEvent(">>> DEFCON 1 DECLARED: TOTAL NUCLEAR WARFARE <<<");
                    }
                    // -------------------------------

                    SimulateAI();
                    Console.Clear();
                    DrawInterface();
                    CheckGameOver();
                    lastTick = DateTime.Now;
                }

                if ((DateTime.Now - lastNewsTick).TotalSeconds > 7)
                {
                    GenerateNews();
                    lastNewsTick = DateTime.Now;
                }

                Thread.Sleep(50);
            }

            ShowScoreboard();
        }

        static void InitializeGameObjects()
        {
            string[] names = {
                "USA", "Russia", "China", "UK", "France", "Germany", "Japan", "India",
                "Iran", "Israel", "Brazil", "Australia", "Canada", "South Korea", "Egypt",
                "Turkey", "Italy", "Spain", "Indonesia", "Mexico", "Saudi Arabia",
                "South Africa", "Argentina", "Ukraine", "Malaysia"
            };

            int[] xCoords = { 4, 43, 46, 23, 24, 26, 58, 38, 36, 33, 14, 53, 4, 56, 30, 28, 26, 22, 48, 2, 33, 30, 12, 30, 50 };
            int[] yCoords = { 6, 4, 9, 5, 6, 6, 8, 10, 9, 8, 15, 15, 3, 8, 9, 7, 7, 7, 13, 9, 10, 16, 16, 5, 12 };

            for (int i = 0; i < names.Length; i++)
            {
                Country newCountry;

                // [REQUIREMENT: INHERITANCE IN ACTION]
                if (names[i] == "USA" || names[i] == "Russia" || names[i] == "China")
                {
                    newCountry = new Superpower(names[i]);
                }
                else
                {
                    newCountry = new Country(names[i]);
                }

                newCountry.SetCoordinates(xCoords[i], yCoords[i]);
                worldCountries.Add(newCountry);
            }
        }

        static void ReadPlayerInput()
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    string commandToProcess = inputCommand.ToUpper().Trim();
                    inputCommand = "";
                    ProcessCommand(commandToProcess);
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (inputCommand.Length > 0)
                    {
                        inputCommand = inputCommand.Substring(0, inputCommand.Length - 1);
                    }
                }
                else
                {
                    inputCommand += keyInfo.KeyChar;
                }

                DrawInputLine();
            }
        }

        static void ProcessCommand(string cmd)
        {
            if (cmd.StartsWith("ATTACK")) StartAttack();
            else if (cmd.StartsWith("AID")) StartAid();
            else if (cmd.StartsWith("REQUEST")) RequestHelp();
            else if (cmd.StartsWith("DRAFT")) StartDraft();
            else if (cmd.StartsWith("HELP")) LogEvent("COMMANDS: ATTACK, AID, REQUEST, DRAFT, QUIT");
            else if (cmd.StartsWith("QUIT")) isGameRunning = false;
            else if (cmd.Length > 0) LogEvent("UNKNOWN COMMAND.");
        }

        static void StartAttack()
        {
            Country target = SelectTargetCountry();
            if (target == null) return;

            Country myCountry = mainPlayer.GetCountry();

            // Self-Attack Check
            if (target.GetName() == myCountry.GetName())
            {
                LogEvent("CANCELLED: YOU CANNOT ATTACK YOUR OWN NATION.");
                return;
            }

            // Betrayal Confirmation
            if (myCountry.HasAlly(target.GetName()))
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("!!! WARNING: BETRAYAL IMMINENT !!!");
                Console.WriteLine($"{target.GetName()} is your ALLY.");
                Console.Write("Are you sure you want to break the alliance and attack? (Y/N): ");

                string confirm = Console.ReadLine().ToUpper();
                if (confirm != "Y")
                {
                    LogEvent($"ATTACK ON {target.GetName()} ABORTED.");
                    return;
                }

                LogEvent($"BETRAYAL! ALLIANCE BROKEN WITH {target.GetName()}!");
                myCountry.RemoveAlly(target.GetName());
                target.RemoveAlly(myCountry.GetName());
            }

            int ecoCost = 15;
            int milCost = 5;
            if (myCountry.GetStats().GetEconomyRaw() < ecoCost || myCountry.GetStats().GetMilitaryRaw() < milCost)
            {
                LogEvent("NOT ENOUGH RESOURCES TO ATTACK.");
                return;
            }

            myCountry.GetStats().ReduceEconomy(ecoCost);
            myCountry.GetStats().ReduceMilitary(milCost);

            Console.Clear();
            DrawInterface();

            bool wasAlive = !target.IsDefeated();
            AnimateMissile(myCountry, target);

            int damage = rng.Next(25, 45);
            target.TakeDamage(damage); // [REQUIREMENT: POLYMORPHISM]
            LogEvent($"ATTACK HIT {target.GetName()} FOR {damage} BASE DAMAGE.");

            if (wasAlive && target.IsDefeated())
            {
                LogEvent($"{target.GetName()} HAS BEEN CONQUERED!");
                mainPlayer.AddConquest();
            }
            else if (!target.IsDefeated())
            {
                LogEvent($"{target.GetName()} IS FIRING BACK!");
                AnimateMissile(target, myCountry);
                myCountry.TakeDamage(rng.Next(10, 20));
            }
        }

        static void StartAid()
        {
            Country myCountry = mainPlayer.GetCountry();
            List<Country> validAllies = new List<Country>();

            foreach (Country c in worldCountries)
            {
                if (myCountry.HasAlly(c.GetName()) && !c.IsDefeated())
                {
                    validAllies.Add(c);
                }
            }

            if (validAllies.Count == 0)
            {
                LogEvent("YOU HAVE NO ACTIVE ALLIES TO AID.");
                return;
            }

            Console.Clear();
            Console.WriteLine("--- SEND AID TO ALLY ---");
            for (int i = 0; i < validAllies.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {validAllies[i].GetName()} (Mil: {validAllies[i].GetStats().GetMilitary():0.0}%)");
            }
            Console.Write("\nSELECT ALLY NUMBER: ");

            // [REQUIREMENT: EXCEPTION HANDLING]
            try
            {
                int choice = int.Parse(Console.ReadLine());
                if (choice <= 0 || choice > validAllies.Count) throw new IndexOutOfRangeException();

                Country target = validAllies[choice - 1];

                if (myCountry.GetStats().GetEconomyRaw() >= 15)
                {
                    myCountry.GetStats().ReduceEconomy(15);
                    target.GetStats().AddMilitary(20);
                    LogEvent($"SENT MILITARY AID TO {target.GetName()}.");
                }
                else
                {
                    LogEvent("INSUFFICIENT ECONOMY TO SEND AID.");
                }
            }
            catch (FormatException)
            {
                LogEvent("ERROR: INVALID INPUT. MUST BE A NUMBER.");
            }
            catch (IndexOutOfRangeException)
            {
                LogEvent("ERROR: ALLY NUMBER OUT OF RANGE.");
            }
        }

        static void RequestHelp()
        {
            // CHECK FOR REMAINING REQUESTS
            if (mainPlayer.GetRequestsRemaining() <= 0)
            {
                LogEvent("REQUEST DENIED: YOU HAVE EXHAUSTED ALL ALLY REQUESTS.");
                return;
            }

            Country myCountry = mainPlayer.GetCountry();
            List<Country> activeAllies = new List<Country>();

            foreach (Country c in worldCountries)
            {
                if (myCountry.HasAlly(c.GetName()) && !c.IsDefeated() && c.GetStats().GetEconomyRaw() > 40)
                {
                    activeAllies.Add(c);
                }
            }

            if (activeAllies.Count > 0)
            {
                Country helper = activeAllies[rng.Next(activeAllies.Count)];
                int amt = rng.Next(10, 25);

                myCountry.GetStats().AddMilitary(amt);
                helper.GetStats().ReduceEconomy(10);

                mainPlayer.UseRequest(); // Deduct a request

                LogEvent($"{helper.GetName()} SENT {amt} SUPPLIES. ({mainPlayer.GetRequestsRemaining()} REQUESTS LEFT)");
            }
            else
            {
                LogEvent("REQUEST DENIED. NO ALLIES CAN HELP RIGHT NOW.");
            }
        }

        static void StartDraft()
        {
            Country myCountry = mainPlayer.GetCountry();
            int popCost = 10;
            int milGain = 15;

            if (myCountry.GetStats().GetPopulationRaw() >= popCost)
            {
                myCountry.GetStats().ReducePopulation(popCost);
                myCountry.GetStats().AddMilitary(milGain);
                LogEvent($"DRAFT SUCCESS: Conscripted {popCost} Pop for {milGain} Military!");
            }
            else
            {
                LogEvent("DRAFT FAILED: Population too low to conscript.");
            }
        }

        static void SimulateAI()
        {
            List<Country> aliveCountries = new List<Country>();
            foreach (Country c in worldCountries)
            {
                if (!c.IsDefeated()) aliveCountries.Add(c);
            }

            // Let the Level Object handle the attacks
            currentLevel.RunLevelMechanics(aliveCountries, mainPlayer.GetCountry(), rng);

            // ==========================================
            // GLOBAL RECOVERY MECHANICS (AI & Player)
            // ==========================================
            foreach (Country nation in aliveCountries)
            {
                Resources stats = nation.GetStats();

                // 1. Passive Economy Growth
                if (stats.GetEconomyRaw() < stats.GetMaxEconomy() && rng.Next(100) < 10)
                {
                    stats.AddEconomy(1);
                }

                // 2. Military Rebuilding
                if (stats.GetEconomyRaw() > 50 && stats.GetMilitaryRaw() < stats.GetMaxMilitary() && rng.Next(100) < 5)
                {
                    stats.ReduceEconomy(2);
                    stats.AddMilitary(1);

                    if (stats.GetMilitaryRaw() == stats.GetMaxMilitary())
                    {
                        LogEvent($"[RECOVERY] {nation.GetName()} has fully rebuilt its military forces.");
                    }
                }
            }
        }

        static void GenerateNews()
        {
            Country randomCountry = worldCountries[rng.Next(worldCountries.Count)];
            if (randomCountry.IsDefeated()) return;

            string headline = "";
            int eventType = rng.Next(3);

            if (eventType == 0)
            {
                headline = $"Market crash in {randomCountry.GetName()} (-15 Eco)";
                randomCountry.GetStats().ReduceEconomy(15);
            }
            else if (eventType == 1)
            {
                headline = $"Mass emigration from {randomCountry.GetName()} (-10 Pop)";
                randomCountry.GetStats().ReducePopulation(10);
            }
            else if (eventType == 2)
            {
                headline = $"{randomCountry.GetName()} invests in military (+15 Mil)";
                randomCountry.GetStats().AddMilitary(15);
            }

            newsFeed.Insert(0, headline);
            if (newsFeed.Count > 15) newsFeed.RemoveAt(newsFeed.Count - 1);
        }

        static void CheckGameOver()
        {
            if (gameHour >= 24.0 || mainPlayer.GetCountry().IsDefeated())
            {
                isGameRunning = false;
            }
        }

        static void ShowScoreboard()
        {
            Console.Clear();
            Country myCountry = mainPlayer.GetCountry();
            Resources stats = myCountry.GetStats();

            int milScore = stats.GetMilitaryRaw() * 10;
            int ecoScore = stats.GetEconomyRaw() * 10;
            int popScore = stats.GetPopulationRaw() * 10;
            int conquestScore = mainPlayer.GetConquests() * 500;

            int totalScore = milScore + ecoScore + popScore + conquestScore;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("                            S I M U L A T I O N   E N D                        ");
            Console.WriteLine("===============================================================================\n");

            if (myCountry.IsDefeated())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (stats.GetMilitaryRaw() <= 0)
                {
                    Console.WriteLine("CRITICAL FAILURE: YOUR MILITARY HAS BEEN WIPED OUT.");
                }
                else
                {
                    Console.WriteLine("CRITICAL FAILURE: YOUR POPULATION HAS BEEN EXTERMINATED.");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("MISSION COMPLETE: YOU SURVIVED 24 HOURS!");
                Console.WriteLine("CONGRATULATIONS COMMANDER.");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n--- FINAL STATISTICS ---");
            string nationType = (myCountry is Superpower) ? "SUPERPOWER" : "STANDARD NATION";
            Console.WriteLine($"NATION:              {myCountry.GetName()} ({nationType})");
            Console.WriteLine($"COUNTRIES CONQUERED: {mainPlayer.GetConquests()} (x500 pts)");

            Console.WriteLine($"FINAL MILITARY:      {stats.GetMilitary():0.0}% (x10 pts)");
            Console.WriteLine($"FINAL ECONOMY:       {stats.GetEconomy():0.0}% (x10 pts)");
            Console.WriteLine($"FINAL POPULATION:    {stats.GetPopulation():0.0}% (x10 pts)");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n===============================================================================");
            Console.WriteLine($" FINAL SCORE: {totalScore}");
            Console.WriteLine("===============================================================================\n");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Press [ENTER] to exit the simulation...");

            while (Console.ReadKey(true).Key != ConsoleKey.Enter) { }
        }

        static void DrawInterface()
        {
            try
            {
                Console.SetCursorPosition(0, 0);
                Console.ForegroundColor = ConsoleColor.Blue;
                string[] map = {
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⣀⣄⣠⣀⡀⣀⣠⣤⣤⣤⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣄⢠⣠⣼⣿⣿⣿⣟⣿⣿⣿⣿⣿⣿⣿⣿⡿⠋⠀⠀⠀⢠⣤⣦⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠰⢦⣄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⣼⣿⣟⣾⣿⣽⣿⣿⣅⠈⠉⠻⣿⣿⣿⣿⣿⡿⠇⠀⠀⠀⠀⠀⠉⠀⠀⠀⠀⠀⢀⡶⠒⢉⡀⢠⣤⣶⣶⣿⣷⣆⣀⡀⠀⢲⣖⠒⠀⠀⠀⠀⠀⠀⠀",
@"⢀⣤⣾⣶⣦⣤⣤⣶⣿⣿⣿⣿⣿⣿⣽⡿⠻⣷⣀⠀⢻⣿⣿⣿⡿⠟⠀⠀⠀⠀⠀⠀⣤⣶⣶⣤⣀⣀⣬⣷⣦⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣶⣦⣤⣦⣼⣀⠀",
@"⠈⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⠛⠓⣿⣿⠟⠁⠘⣿⡟⠁⠀⠘⠛⠁⠀⠀⢠⣾⣿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⠏⠙⠁",
@"⠀⠸⠟⠋⠀⠈⠙⣿⣿⣿⣿⣿⣿⣷⣦⡄⣿⣿⣿⣆⠀⠀⠀⠀⠀⠀⠀⠀⣼⣆⢘⣿⣯⣼⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡉⠉⢱⡿⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠘⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣟⡿⠦⠀⠀⠀⠀⠀⠀⠀⠙⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⡗⠀⠈⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⢻⣿⣿⣿⣿⣿⣿⣿⣿⠋⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⢿⣿⣉⣿⡿⢿⢷⣾⣾⣿⣞⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠋⣠⠟⠀⠀⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠹⣿⣿⣿⠿⠿⣿⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣀⣾⣿⣿⣷⣦⣶⣦⣼⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⠈⠛⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠻⣿⣤⡖⠛⠶⠤⡀⠀⠀⠀⠀⠀⠀⠀⢰⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⠁⠙⣿⣿⠿⢻⣿⣿⡿⠋⢩⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⠧⣤⣦⣤⣄⡀⠀⠀⠀⠀⠀⠘⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡇⠀⠀⠀⠘⣧⠀⠈⣹⡻⠇⢀⣿⡆⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢠⣿⣿⣿⣿⣿⣤⣀⡀⠀⠀⠀⠀⠀⠀⠈⢽⣿⣿⣿⣿⣿⠋⠀⠀⠀⠀⠀⠀⠀⠀⠹⣷⣴⣿⣷⢲⣦⣤⡀⢀⡀⠀⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⢿⣿⣿⣿⣿⣿⣿⠟⠀⠀⠀⠀⠀⠀⠀⢸⣿⣿⣿⣿⣷⢀⡄⠀⠀⠀⠀⠀⠀⠀⠀⠈⠉⠂⠛⣆⣤⡜⣟⠋⠙⠂⠀⠀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢹⣿⣿⣿⣿⠟⠀⠀⠀⠀⠀⠀⠀⠀⠘⣿⣿⣿⣿⠉⣿⠃⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣤⣾⣿⣿⣿⣿⣆⠀⠰⠄⠀⠉⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣸⣿⣿⡿⠃⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢹⣿⡿⠃⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢻⣿⠿⠿⣿⣿⣿⠇⠀⠀⢀⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⣿⡿⠛⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⢻⡇⠀⠀⢀⣼⠗⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⣿⠃⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⠁⠀⠀⠀",
@"⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⠒⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀"
            };
                foreach (string line in map) Console.WriteLine(line);

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("===============================================================================");
                Console.WriteLine($"TIME: {gameHour:00.00} / 24.00 HOURS   |   DEFCON: {defconLevel}");
                Console.WriteLine($"STAGE: {currentLevel.LevelName}");

                Console.WriteLine($"MILITARY: {mainPlayer.GetCountry().GetStats().GetMilitary():0.0}%  |  ECONOMY: {mainPlayer.GetCountry().GetStats().GetEconomy():0.0}%  |  POP: {mainPlayer.GetCountry().GetStats().GetPopulation():0.0}%");

                string allyString = string.Join(", ", mainPlayer.GetCountry().GetAllies());
                if (allyString == "") allyString = "NONE";
                Console.WriteLine($"ALLIES: {allyString} (Requests Left: {mainPlayer.GetRequestsRemaining()})");

                Console.WriteLine("===============================================================================");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("COMMANDS MANUAL:");
                Console.WriteLine(" [ATTACK]  - Launch an offensive strike (Cost: 15 Eco, 5 Mil)");
                Console.WriteLine(" [AID]     - Send military supplies to allied nations (Cost: 15 Eco)");
                Console.WriteLine($" [REQUEST] - Ask your allies for supplies (Cost: 1 Request Token)");
                Console.WriteLine(" [DRAFT]   - Conscript citizens into the military (Cost: 10 Pop, Gain: 15 Mil)");
                Console.WriteLine(" [QUIT]    - Exit the simulation");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("-------------------------------------------------------------------------------");

                Console.WriteLine("--- LATEST GLOBAL NEWS ---");
                for (int i = 0; i < 3; i++)
                {
                    if (i < newsFeed.Count) Console.WriteLine("* " + newsFeed[i].PadRight(70));
                    else Console.WriteLine(new string(' ', 75));
                }

                Console.WriteLine("\n--- TACTICAL LOG ---");
                for (int i = 0; i < 3; i++)
                {
                    if (i < eventLog.Count) Console.WriteLine("> " + eventLog[eventLog.Count - 1 - i].PadRight(70));
                    else Console.WriteLine(new string(' ', 75));
                }

                DrawInputLine();
            }
            catch { }
        }

        static void DrawInputLine()
        {
            try
            {
                Console.SetCursorPosition(0, 40);
                Console.Write(new string(' ', 95));
                Console.SetCursorPosition(0, 40);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"COMMAND > {inputCommand}_");
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch { }
        }

        static Country SelectTargetCountry()
        {
            try
            {
                Console.Clear();
                Console.WriteLine("--- SELECT TARGET ---");
                for (int i = 0; i < worldCountries.Count; i++)
                {
                    if (!worldCountries[i].IsDefeated() && worldCountries[i].GetName() != mainPlayer.GetCountry().GetName())
                    {
                        string typeMarker = (worldCountries[i] is Superpower) ? "[SUPERPOWER]" : "";
                        Console.WriteLine($"{i + 1}. {worldCountries[i].GetName()} {typeMarker} (Mil: {worldCountries[i].GetStats().GetMilitary():0.0}%)");
                    }
                }
                Console.Write("\nENTER NUMBER: ");

                int targetId = int.Parse(Console.ReadLine());

                if (targetId <= 0 || targetId > worldCountries.Count)
                {
                    throw new IndexOutOfRangeException();
                }

                return worldCountries[targetId - 1];
            }
            catch (FormatException)
            {
                LogEvent("ERROR: PLEASE ENTER A VALID NUMBER, NOT TEXT.");
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                LogEvent("ERROR: SELECTION OUT OF RANGE.");
                return null;
            }
        }

        static Player SelectCountryScreen()
        {
            while (true)
            {
                try
                {
                    Console.Clear();
                    Console.WriteLine("=== CHOOSE YOUR NATION ===");
                    for (int i = 0; i < worldCountries.Count; i++)
                    {
                        string typeMarker = (worldCountries[i] is Superpower) ? "[SUPERPOWER]" : "";
                        Console.WriteLine($"{i + 1}. {worldCountries[i].GetName()} {typeMarker}");
                    }
                    Console.Write("\nSELECTION > ");

                    int choice = int.Parse(Console.ReadLine());

                    if (choice < 1 || choice > worldCountries.Count)
                    {
                        throw new IndexOutOfRangeException();
                    }

                    Country pickedCountry = worldCountries[choice - 1];
                    Player newPlayer = new Player(pickedCountry);
                    return newPlayer;
                }
                catch (FormatException)
                {
                    Console.WriteLine("\nINVALID INPUT! Please enter a valid number. Press ENTER to try again.");
                    Console.ReadLine();
                }
                catch (IndexOutOfRangeException)
                {
                    Console.WriteLine("\nSELECTION OUT OF RANGE! Press ENTER to try again.");
                    Console.ReadLine();
                }
            }
        }

        static void SetupConsole()
        {
            Console.Title = "SENTINEL";
            Console.CursorVisible = false;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            try
            {
                Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
                Console.SetBufferSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
            }
            catch
            {
                try
                {
                    Console.SetWindowSize(100, 43);
                    Console.SetBufferSize(100, 43);
                }
                catch { }
            }
        }

        public static void LogEvent(string message)
        {
            eventLog.Add(message);
        }

        static void AnimateMissile(Country start, Country end)
        {
            int startX = start.GetMapX();
            int startY = start.GetMapY();
            int endX = end.GetMapX();
            int endY = end.GetMapY();

            int steps = 10;
            for (int i = 0; i <= steps; i++)
            {
                int currentX = startX + ((endX - startX) * i / steps);
                int currentY = startY + ((endY - startY) * i / steps);

                try
                {
                    Console.SetCursorPosition(currentX, currentY);

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("X");
                    Console.ForegroundColor = ConsoleColor.White;

                    Thread.Sleep(50);
                }
                catch { }
            }
        }
    }

    // ================= FULLY ENCAPSULATED CLASSES =================

    interface IAttackable
    {
        void TakeDamage(int damageAmount);
        bool IsDefeated();
    }

    class Resources
    {
        private int military;
        private int maxMilitary;
        private int economy;
        private int maxEconomy;
        private int population;
        private int maxPopulation;

        public Resources(int milCap = 100, int ecoCap = 100, int popCap = 100)
        {
            maxMilitary = milCap;
            military = milCap;
            maxEconomy = ecoCap;
            economy = ecoCap;
            maxPopulation = popCap;
            population = popCap;
        }

        public double GetMilitary() { return ((double)military / maxMilitary) * 100.0; }
        public double GetEconomy() { return ((double)economy / maxEconomy) * 100.0; }
        public double GetPopulation() { return ((double)population / maxPopulation) * 100.0; }

        public int GetMilitaryRaw() { return military; }
        public int GetEconomyRaw() { return economy; }
        public int GetPopulationRaw() { return population; }
        public int GetMaxMilitary() { return maxMilitary; }
        public int GetMaxEconomy() { return maxEconomy; }
        public int GetMaxPopulation() { return maxPopulation; }

        public void ReduceMilitary(int amount)
        {
            military -= amount;
            if (military < 0) military = 0;
        }

        public void AddMilitary(int amount)
        {
            military += amount;
            if (military > maxMilitary) military = maxMilitary;
        }

        public void ReduceEconomy(int amount)
        {
            economy -= amount;
            if (economy < 0) economy = 0;
        }

        public void AddEconomy(int amount)
        {
            economy += amount;
            if (economy > maxEconomy) economy = maxEconomy;
        }

        public void ReducePopulation(int amount)
        {
            population -= amount;
            if (population < 0) population = 0;
        }
    }

    class Country : IAttackable
    {
        private string name;
        private Resources stats;
        private List<string> allies;
        private int mapX;
        private int mapY;

        // Default capacities passed to Resources
        public Country(string countryName, int milCapacity = 100, int ecoCapacity = 100, int popCapacity = 100)
        {
            name = countryName;
            stats = new Resources(milCapacity, ecoCapacity, popCapacity);
            allies = new List<string>();
        }

        public string GetName() { return name; }
        public Resources GetStats() { return stats; }
        public int GetMapX() { return mapX; }
        public int GetMapY() { return mapY; }
        public int GetAllyCount() { return allies.Count; }
        public List<string> GetAllies() { return allies; }

        public void SetCoordinates(int x, int y)
        {
            mapX = x;
            mapY = y;
        }

        public void AddAlly(string allyName)
        {
            allies.Add(allyName);
        }

        public void RemoveAlly(string allyName)
        {
            allies.Remove(allyName);
        }

        public bool HasAlly(string checkName)
        {
            return allies.Contains(checkName);
        }

        public bool IsDefeated()
        {
            if (stats.GetMilitaryRaw() <= 0 || stats.GetPopulationRaw() <= 0)
            {
                return true;
            }
            return false;
        }

        public virtual void TakeDamage(int damageAmount)
        {
            stats.ReduceMilitary(damageAmount);
            stats.ReduceEconomy(damageAmount / 2);
            stats.ReducePopulation(damageAmount / 5);
        }
    }

    class Superpower : Country
    {
        // Constructor Chaining: Upgrading capacities to 150 Mil, 150 Eco, and 200 Pop!
        public Superpower(string countryName) : base(countryName, 150, 150, 200)
        {
        }

        public override void TakeDamage(int damageAmount)
        {
            int reducedDamage = (int)(damageAmount * 0.75);
            Program.LogEvent($"{this.GetName()}'S MISSILE DEFENSE BLOCKED 25% OF DAMAGE!");
            base.TakeDamage(reducedDamage);
        }
    }

    class Player
    {
        private Country controlledCountry;
        private int countriesConquered;
        private int requestsRemaining;

        public Player(Country selectedCountry)
        {
            controlledCountry = selectedCountry;
            countriesConquered = 0;
            requestsRemaining = 3;
        }

        public Country GetCountry()
        {
            return controlledCountry;
        }

        public void AddConquest()
        {
            countriesConquered++;
        }

        public int GetConquests()
        {
            return countriesConquered;
        }

        public int GetRequestsRemaining()
        {
            return requestsRemaining;
        }

        public void UseRequest()
        {
            requestsRemaining--;
        }
    }

    // ================= GAME LEVELS =================

    abstract class GameLevel
    {
        public string LevelName { get; protected set; }
        public abstract void RunLevelMechanics(List<Country> aliveCountries, Country playerCountry, Random rng);
    }

    class Defcon3Level : GameLevel
    {
        public Defcon3Level() { LevelName = "STAGE 1: RISING TENSIONS"; }

        public override void RunLevelMechanics(List<Country> aliveCountries, Country playerCountry, Random rng)
        {
            if (aliveCountries.Count < 2) return;

            if (rng.Next(100) < 10)
            {
                Country attacker = aliveCountries[rng.Next(aliveCountries.Count)];
                Country victim = aliveCountries[rng.Next(aliveCountries.Count)];

                if (attacker.GetName() != victim.GetName() &&
                    attacker.GetName() != playerCountry.GetName())
                {
                    victim.TakeDamage(rng.Next(10, 25));
                    Program.LogEvent($"[SKIRMISH] {attacker.GetName()} fired on {victim.GetName()}!");
                }
            }
        }
    }

    class Defcon2Level : GameLevel
    {
        public Defcon2Level() { LevelName = "STAGE 2: BRINK OF WAR"; }

        public override void RunLevelMechanics(List<Country> aliveCountries, Country playerCountry, Random rng)
        {
            if (aliveCountries.Count < 2) return;

            if (rng.Next(100) < 20)
            {
                Country attacker = aliveCountries[rng.Next(aliveCountries.Count)];
                Country victim = aliveCountries[rng.Next(aliveCountries.Count)];

                if (attacker.GetName() != victim.GetName() &&
                    attacker.GetName() != playerCountry.GetName())
                {
                    victim.TakeDamage(rng.Next(20, 35));
                    Program.LogEvent($"[HEAVY STRIKE] {attacker.GetName()} bombed {victim.GetName()}!");
                }
            }

            if (rng.Next(100) < 5)
            {
                Program.LogEvent("GLOBAL PANIC: Citizens are fleeing! (-2 Pop)");
                foreach (Country c in aliveCountries) c.GetStats().ReducePopulation(2);
            }
        }
    }

    class Defcon1Level : GameLevel
    {
        public Defcon1Level() { LevelName = "STAGE 3: TOTAL WARFARE"; }

        public override void RunLevelMechanics(List<Country> aliveCountries, Country playerCountry, Random rng)
        {
            if (aliveCountries.Count < 2) return;

            if (rng.Next(100) < 35)
            {
                Country attacker = aliveCountries[rng.Next(aliveCountries.Count)];
                Country victim = aliveCountries[rng.Next(aliveCountries.Count)];

                if (attacker.GetName() != victim.GetName() &&
                    attacker.GetName() != playerCountry.GetName())
                {
                    victim.TakeDamage(rng.Next(30, 50));
                    Program.LogEvent($"[NUCLEAR LAUNCH] {attacker.GetName()} decimated {victim.GetName()}!");
                }
            }

            if (rng.Next(100) < 10)
            {
                Program.LogEvent("MARKET COLLAPSE: Global war destroys supply chains! (-5 Eco)");
                foreach (Country c in aliveCountries) c.GetStats().ReduceEconomy(5);
            }
        }
    }
}

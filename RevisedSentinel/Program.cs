using System;
using System.Collections.Generic;
using System.Threading;

namespace Sentinel
{
    // ================= MAIN PROGRAM =================
    class Program
    {
        // Global variables for the game state
        static List<Country> worldCountries = new List<Country>();
        static Player mainPlayer;

        static bool isGameRunning = true;
        static double gameHour = 0;
        static int defconLevel = 5;

        static string inputCommand = "";
        static DateTime lastTick = DateTime.Now;
        // Adjusted to 7 seconds
        static DateTime lastNewsTick = DateTime.Now.AddSeconds(-7);
        static Random rng = new Random();
        static List<string> newsFeed = new List<string>();
        static List<string> eventLog = new List<string>();

        static void Main()
        {
            SetupConsole();
            InitializeGameObjects();

            mainPlayer = SelectCountryScreen();

            // Assign exactly 2 allies only to the player's chosen country for simplicity
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
                    // 1.75x Speed Multiplier
                    gameHour += (1.75 / 60.0);
                    SimulateAI();
                    Console.Clear();
                    DrawInterface();
                    CheckGameOver();
                    lastTick = DateTime.Now;
                }

                // Generates news every 7 seconds
                if ((DateTime.Now - lastNewsTick).TotalSeconds > 7)
                {
                    GenerateNews();
                    lastNewsTick = DateTime.Now;
                }

                Thread.Sleep(50);
            }

            // Show the final score when the loop breaks!
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

            // FIXED: Shifted every X coordinate to the left by 12 spaces to align with the ASCII map
            int[] xCoords = { 4, 43, 46, 23, 24, 26, 58, 38, 36, 33, 14, 53, 4, 56, 30, 28, 26, 22, 48, 2, 33, 30, 12, 30, 50 };
            int[] yCoords = { 6, 4, 9, 5, 6, 6, 8, 10, 9, 8, 15, 15, 3, 8, 9, 7, 7, 7, 13, 9, 10, 16, 16, 5, 12 };

            for (int i = 0; i < names.Length; i++)
            {
                Country newCountry = new Country(names[i]);
                newCountry.SetCoordinates(xCoords[i], yCoords[i]);
                worldCountries.Add(newCountry);
            }
        }

        // ================= INPUT & COMMANDS =================
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
            else if (cmd.StartsWith("HELP")) LogEvent("COMMANDS: ATTACK, AID, REQUEST, QUIT");
            else if (cmd.StartsWith("QUIT")) isGameRunning = false;
            else if (cmd.Length > 0) LogEvent("UNKNOWN COMMAND.");
        }

        static void StartAttack()
        {
            Country target = SelectTargetCountry();
            if (target == null) return;

            int ecoCost = 15;
            int milCost = 5;
            Country myCountry = mainPlayer.GetCountry();

            if (myCountry.GetStats().GetEconomy() < ecoCost || myCountry.GetStats().GetMilitary() < milCost)
            {
                LogEvent("NOT ENOUGH RESOURCES TO ATTACK.");
                return;
            }

            myCountry.GetStats().ReduceEconomy(ecoCost);
            myCountry.GetStats().ReduceMilitary(milCost);

            if (myCountry.HasAlly(target.GetName()))
            {
                LogEvent($"ALLIANCE BROKEN WITH {target.GetName()}!");
                myCountry.RemoveAlly(target.GetName());
            }

            Console.Clear();
            DrawInterface();

            bool wasAlive = !target.IsDefeated();

            AnimateMissile(myCountry, target);
            int damage = rng.Next(25, 45);
            target.TakeDamage(damage);
            LogEvent($"ATTACK HIT {target.GetName()} FOR {damage} DAMAGE.");

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
            Country target = SelectTargetCountry();
            Country myCountry = mainPlayer.GetCountry();

            if (target != null && myCountry.GetStats().GetEconomy() >= 15)
            {
                myCountry.GetStats().ReduceEconomy(15);
                target.GetStats().AddMilitary(20);
                LogEvent($"SENT AID TO {target.GetName()}.");
            }
        }

        static void RequestHelp()
        {
            Country myCountry = mainPlayer.GetCountry();

            // Find all alive allies who have enough economy to help
            List<Country> activeAllies = new List<Country>();
            foreach (Country c in worldCountries)
            {
                if (myCountry.HasAlly(c.GetName()) && !c.IsDefeated() && c.GetStats().GetEconomy() > 40)
                {
                    activeAllies.Add(c);
                }
            }

            // Pick a random ally to help us
            if (activeAllies.Count > 0)
            {
                Country helper = activeAllies[rng.Next(activeAllies.Count)];
                int amt = rng.Next(10, 25);

                myCountry.GetStats().AddMilitary(amt);
                helper.GetStats().ReduceEconomy(10);

                LogEvent($"{helper.GetName()} SENT {amt} SUPPLIES.");
            }
            else
            {
                LogEvent("REQUEST DENIED. NO ALLIES CAN HELP.");
            }
        }

        // ================= SIMULATION LOGIC =================
        static void SimulateAI()
        {
            List<Country> aliveCountries = new List<Country>();
            foreach (Country c in worldCountries)
            {
                if (!c.IsDefeated())
                {
                    aliveCountries.Add(c);
                }
            }

            if (aliveCountries.Count < 2) return;

            if (rng.Next(100) < 10)
            {
                Country attacker = aliveCountries[rng.Next(aliveCountries.Count)];
                Country victim = aliveCountries[rng.Next(aliveCountries.Count)];
                Country myCountry = mainPlayer.GetCountry();

                if (attacker.GetName() != victim.GetName() && attacker.GetName() != myCountry.GetName())
                {
                    AnimateMissile(attacker, victim);
                    victim.TakeDamage(rng.Next(15, 30));
                }
            }

            if (mainPlayer.GetCountry().GetStats().GetEconomy() < 100 && rng.Next(100) < 15)
            {
                mainPlayer.GetCountry().GetStats().AddEconomy(1);
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
                headline = $"Protests in {randomCountry.GetName()} (-10 Stability)";
                randomCountry.GetStats().ReduceStability(10);
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

            int milScore = stats.GetMilitary() * 10;
            int ecoScore = stats.GetEconomy() * 10;
            int popScore = stats.GetPopulation() * 10;
            int conquestScore = mainPlayer.GetConquests() * 500;

            int totalScore = milScore + ecoScore + popScore + conquestScore;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("                            S I M U L A T I O N   E N D                        ");
            Console.WriteLine("===============================================================================\n");

            if (myCountry.IsDefeated())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("CRITICAL FAILURE: YOUR NATION HAS FALLEN.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("MISSION COMPLETE: YOU SURVIVED 24 HOURS!");
                Console.WriteLine("CONGRATULATIONS COMMANDER.");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n--- FINAL STATISTICS ---");
            Console.WriteLine($"NATION:              {myCountry.GetName()}");
            Console.WriteLine($"COUNTRIES CONQUERED: {mainPlayer.GetConquests()} (x500 pts)");
            Console.WriteLine($"FINAL MILITARY:      {stats.GetMilitary()}% (x10 pts)");
            Console.WriteLine($"FINAL ECONOMY:       {stats.GetEconomy()}% (x10 pts)");
            Console.WriteLine($"FINAL POPULATION:    {stats.GetPopulation()}% (x10 pts)");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n===============================================================================");
            Console.WriteLine($" FINAL SCORE: {totalScore}");
            Console.WriteLine("===============================================================================\n");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Press [ENTER] to exit the simulation...");

            while (Console.ReadKey(true).Key != ConsoleKey.Enter) { }
        }

        // ================= UI AND GRAPHICS =================
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
                Console.WriteLine($"MILITARY: {mainPlayer.GetCountry().GetStats().GetMilitary()}%  |  ECONOMY: {mainPlayer.GetCountry().GetStats().GetEconomy()}%");

                string allyString = string.Join(", ", mainPlayer.GetCountry().GetAllies());
                if (allyString == "") allyString = "NONE";
                Console.WriteLine($"ALLIES: {allyString}");

                Console.WriteLine("===============================================================================");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("COMMANDS MANUAL:");
                Console.WriteLine(" [ATTACK]  - Launch an offensive strike (Cost: 15 Eco, 5 Mil)");
                Console.WriteLine(" [AID]     - Send military supplies to allied nations (Cost: 15 Eco)");
                Console.WriteLine(" [REQUEST] - Ask your allies for military supplies");
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
                Console.SetCursorPosition(0, 38);
                Console.Write(new string(' ', 95));
                Console.SetCursorPosition(0, 38);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"COMMAND > {inputCommand}_");
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch { }
        }

        static Country SelectTargetCountry()
        {
            Console.Clear();
            Console.WriteLine("--- SELECT TARGET ---");
            for (int i = 0; i < worldCountries.Count; i++)
            {
                if (!worldCountries[i].IsDefeated())
                {
                    Console.WriteLine($"{i + 1}. {worldCountries[i].GetName()} (Mil: {worldCountries[i].GetStats().GetMilitary()})");
                }
            }
            Console.Write("\nENTER NUMBER: ");
            string choice = Console.ReadLine();

            int targetId;
            if (int.TryParse(choice, out targetId) && targetId > 0 && targetId <= worldCountries.Count)
            {
                return worldCountries[targetId - 1];
            }
            return null;
        }

        static Player SelectCountryScreen()
        {
            Console.Clear();
            Console.WriteLine("=== CHOOSE YOUR NATION ===");
            for (int i = 0; i < worldCountries.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {worldCountries[i].GetName()}");
            }
            Console.Write("\nSELECTION > ");

            int choice = 1;
            int.TryParse(Console.ReadLine(), out choice);
            if (choice < 1 || choice > worldCountries.Count) choice = 1;

            Country pickedCountry = worldCountries[choice - 1];
            Player newPlayer = new Player(pickedCountry);
            return newPlayer;
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
                    Console.SetWindowSize(100, 40);
                    Console.SetBufferSize(100, 40);
                }
                catch { }
            }
        }

        static void LogEvent(string message)
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

    // Class 1: Resources
    class Resources
    {
        private int military;
        private int economy;
        private int population;
        private int stability;

        public Resources()
        {
            military = 100;
            economy = 100;
            population = 100;
            stability = 100;
        }

        public int GetMilitary() { return military; }
        public int GetEconomy() { return economy; }
        public int GetPopulation() { return population; }
        public int GetStability() { return stability; }

        public void ReduceMilitary(int amount)
        {
            military -= amount;
            if (military < 0) military = 0;
        }

        public void AddMilitary(int amount)
        {
            military += amount;
            if (military > 100) military = 100;
        }

        public void ReduceEconomy(int amount)
        {
            economy -= amount;
            if (economy < 0) economy = 0;
        }

        public void AddEconomy(int amount)
        {
            economy += amount;
            if (economy > 100) economy = 100;
        }

        public void ReducePopulation(int amount)
        {
            population -= amount;
            if (population < 0) population = 0;
        }

        public void ReduceStability(int amount)
        {
            stability -= amount;
            if (stability < 0) stability = 0;
        }
    }

    // Class 2: Country
    class Country
    {
        private string name;
        private Resources stats;
        private List<string> allies;
        private int mapX;
        private int mapY;

        public Country(string countryName)
        {
            name = countryName;
            stats = new Resources();
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
            if (stats.GetMilitary() <= 0)
            {
                return true;
            }
            return false;
        }

        public void TakeDamage(int damageAmount)
        {
            stats.ReduceMilitary(damageAmount);
            stats.ReduceEconomy(damageAmount / 2);
            stats.ReducePopulation(damageAmount / 5);
        }
    }

    // Class 3: Player
    class Player
    {
        private Country controlledCountry;
        private int countriesConquered;

        public Player(Country selectedCountry)
        {
            controlledCountry = selectedCountry;
            countriesConquered = 0;
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
    }
}
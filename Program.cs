using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Win32;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using NexusMods.Paths;

namespace COM3D2_DLC_Checker
{

    class Program
    {

        // Variables
        static readonly string DLC_URL = "https://raw.githubusercontent.com/krypto5863/COM3D2_DLC_Checker/master/COM_NewListDLC.lst";
        static readonly string DLC_LIST_PATH = Path.Combine(Directory.GetCurrentDirectory(), "COM_NewListDLC.lst");

        static readonly uint STEAM_APPID_COM3D2INM = 1097580; // CUSTOM ORDER MAID 3D2 It's a Night Magic

        static void Main(string[] args)
        {
            PRINT_HEADER();

            // HTTP_RESOPOND
            //  - Item1 = HTTP Status Code
            //  - Item2 = Internet DLC List content
            var HTTP_RESPOND = CONNECT_TO_INTERNET(DLC_URL);

            if (HTTP_RESPOND.Item1 == HttpStatusCode.OK)
            {
                Console.WriteLine("Connected to {0}", DLC_URL);
                UPDATE_DLC_LIST(HTTP_RESPOND.Item2);
            }
            else
            {
                Console.WriteLine("Can't connect to internet, offline file will be used");
            }

            // DLC LIST = [DLC_FILENAME, DLC_NAME]
            var DLC_LIST = READ_DLC_LIST();
            var GAMEDATA_LIST = READ_GAMEDATA();

            // DLC LIST SORTED
            // Item 1 = INSTALLED_DLC
            // Item 2 = NOT_INSTALLED_DLC
            var DLC_LIST_SORTED = COMPARE_DLC(DLC_LIST, GAMEDATA_LIST);

            PRINT_DLC(DLC_LIST_SORTED.Item1, DLC_LIST_SORTED.Item2);

            EXIT_PROGRAM();
        }

        static void PRINT_HEADER()
        {
            CONSOLE_COLOR(ConsoleColor.Cyan, "===========================================================================================");
            CONSOLE_COLOR(ConsoleColor.Cyan, "COM_DLC_Checker (Kry Fork)     |   Github.com/krypto5863/COM3D2_DLC_Checker");
            CONSOLE_COLOR(ConsoleColor.Cyan, "===========================================================================================");
        }

        static Tuple<HttpStatusCode, string> CONNECT_TO_INTERNET(string DLC_URL)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(DLC_URL);
            var request = httpWebRequest;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            try
            {
                using var response = (HttpWebResponse)request.GetResponse();
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);

                return new Tuple<HttpStatusCode, string>(response.StatusCode, reader.ReadToEnd());
            }
            catch (System.Net.WebException){
                return new Tuple<HttpStatusCode, string>(HttpStatusCode.NotFound, null);
            }

        }

        static void UPDATE_DLC_LIST(string UPDATED_CONTENT)
        {
            using var writer = new StreamWriter(DLC_LIST_PATH);
            writer.Write(UPDATED_CONTENT);
        }

        static IDictionary<string, string> READ_DLC_LIST()
        {
            var DLC_LIST_UNFORMATED = new List<string>();

            try
            {
                // Skip 1 = Remove version header
                DLC_LIST_UNFORMATED = File.ReadAllLines(DLC_LIST_PATH, Encoding.UTF8)
                    .Skip(1)
                    .ToList();

            }
            catch(FileNotFoundException)
            {
                CONSOLE_COLOR(ConsoleColor.Red, "COM_NewListDLC.lst file doesn't exist, Connect to the internet to download it automatically");
                EXIT_PROGRAM();
            }

            // DLC_LIST_FORMAT = [Keys = DLC_Filename, Value = DLC_Name]
            IDictionary<string, string> DLC_LIST_FORMATED = new Dictionary<string, string>();

            foreach (var DLC_LIST in DLC_LIST_UNFORMATED)
            {
                var temp_strlist = DLC_LIST.Split(',');
                DLC_LIST_FORMATED.Add(temp_strlist[0], temp_strlist[1]);
            }

            return DLC_LIST_FORMATED;

        }

        static string GET_COM3D2_INSTALLPATH()
        {
            // Default: Current Directory of COM3D2_DLC_Checker
            // Will replaced by COM3D2 InstallPath Registry
            const string keyName = "HKEY_CURRENT_USER" + "\\" + "SOFTWARE\\KISS\\カスタムオーダーメイド3D2";

            var GAME_DIRECTORY_REGISTRY = (string)Registry.GetValue(keyName,"InstallPath","");

            if (GAME_DIRECTORY_REGISTRY != null && Directory.Exists(GAME_DIRECTORY_REGISTRY) && File.Exists(Path.Combine(GAME_DIRECTORY_REGISTRY, "COM3D2x64.exe")))
            {
                return GAME_DIRECTORY_REGISTRY;
            }

            var handler = new SteamHandler(FileSystem.Shared, WindowsRegistry.Shared);
            if (handler != null)
            {
                var comd3d2 = handler.FindOneGameById(AppId.From(STEAM_APPID_COM3D2INM), out var errors1);
                if (comd3d2 != null)
                {
                    string com3d2inmPath = Path.Combine(comd3d2.Path.ToString(), @"com3d2inm");
                    return com3d2inmPath;
                }
            }

            CONSOLE_COLOR(ConsoleColor.Yellow, "Warning : COM3D2 installation directory is not set or is set improperly in the registry. Will use current directory");
            return Directory.GetCurrentDirectory();
        }

        static List<string> READ_GAMEDATA()
        {
            var GAME_DIRECTORY = GET_COM3D2_INSTALLPATH();
            var GAMEDATA_DIRECTORY = GAME_DIRECTORY + "\\GameData";
            var GAMEDATA_20_DIRECTORY = GAME_DIRECTORY + "\\GameData_20";

            var GAMEDATA_LIST = new List<string>();

            GAMEDATA_LIST.AddRange(Directory.GetFiles(@GAMEDATA_DIRECTORY, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName));
            GAMEDATA_LIST.AddRange(Directory.GetFiles(@GAMEDATA_20_DIRECTORY, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName));

            return GAMEDATA_LIST;
        }

        static Tuple<List<string>,List<string>> COMPARE_DLC(IDictionary<string, string> DLC_LIST, List<string> GAMEDATA_LIST)
        {
            // DLC LIST = [DLC_FILENAME, DLC_NAME]
            var DLC_FILENAMES = new List<string>(DLC_LIST.Keys);
            var DLC_NAMES= new List<string>(DLC_LIST.Values);

            var INSTALLED_DLC = new List<string>(); 
            foreach(var INSTALLED_DLC_FILENAMES in DLC_FILENAMES.Intersect(GAMEDATA_LIST).ToList())
            {
                // UNIT_DLC_LIST = [DLC_FILENAME, DLC_NAME]
                foreach (var UNIT_DLC_LIST in DLC_LIST)
                {
                    if (INSTALLED_DLC_FILENAMES == UNIT_DLC_LIST.Key)
                    {
                        INSTALLED_DLC.Add(UNIT_DLC_LIST.Value);
                        DLC_LIST.Remove(UNIT_DLC_LIST);
                        break;
                    }
                }
            }
            
            var NOT_INSTALLED_DLC = DLC_NAMES.Except(INSTALLED_DLC).ToList();
            INSTALLED_DLC.Sort();
            NOT_INSTALLED_DLC.Sort();
            return Tuple.Create(INSTALLED_DLC, NOT_INSTALLED_DLC);
        }

        static void PRINT_DLC(List<string> INSTALLED_DLC, List<string> NOT_INSTALLED_DLC)
        {
            CONSOLE_COLOR(ConsoleColor.Cyan, "\nAlready Installed:");
            foreach (var DLC in INSTALLED_DLC)
            {
                Console.WriteLine(DLC);
            }

            CONSOLE_COLOR(ConsoleColor.Cyan, "\nNot Installed :");
            foreach (var DLC in NOT_INSTALLED_DLC)
            {
                Console.WriteLine(DLC);
            }
        }

        static void EXIT_PROGRAM()
        {
            Console.WriteLine("\nPress 'Enter' to exit the process...");
            while (true)
            {
                if (Console.ReadKey().Key == ConsoleKey.Enter)
                {
                    System.Environment.Exit(0);
                }
            }
        }

        // Extension
        static void CONSOLE_COLOR(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}

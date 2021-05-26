using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace APChiaPlotManager
{
    class ProgramV1
    {
        private const string _formatDate = "dd/MM/yyyy HH:mm:ss";
        private const string _version = "v.1.0.0";

        static void Main2(string[] args)
        {
            try
            {
                Console.WriteLine("----------------------------------------------------------------------------------------");
                Console.WriteLine($"{Now()} Start AP Chia Plot Manager ({_version}) by grafbcn");
                Console.WriteLine("----------------------------------------------------------------------------------------");
                bool goExit = GetArgs(args, out List<string> originPaths, out string destinationPath, out int method, out int bufferSize);
                if (goExit)
                {
                    ShowInfo();
                }
                else
                {
                    StartThreadManager(originPaths, destinationPath, method, bufferSize);
                }
                Console.WriteLine($"{Now()} End!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.WriteLine($"{Now()} Pulse 'Enter' to exit!");
            Console.ReadLine();
        }

        private static void StartThreadManager(List<string> originFolders, string destinationFolder, int method, int bufferSize)
        {
            bool goExit = false;
            Console.WriteLine($"\n{Now()} Start thread CPM ({_version})\n\tSources: {string.Join(';', originFolders)}\n\tDestination: {destinationFolder}");
            while (!goExit)
            {
                // Check new files with type *.plot
                if (!originFolders.Any(op => Directory.Exists(op)))
                {
                    Console.WriteLine($"{Now()} ERROR: Don't exist any folder {string.Join(';', originFolders)} KO!");
                    goExit = true;
                }
                else if (!Directory.Exists(destinationFolder))
                {
                    Console.WriteLine($"{Now()} WARNING: Destination folder KO! (not exist)");
                    try
                    {
                        Console.WriteLine($"{Now()} Try create destination folder '{destinationFolder}'");
                        Directory.CreateDirectory(destinationFolder);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{Now()} ERROR: {ex.Message}");
                    }
                    if (!Directory.Exists(destinationFolder))
                    {
                        Console.WriteLine($"{Now()} ERROR: Destination folder KO! (not exist). Create folder not worked");
                        goExit = true;
                    }
                }

                if (!goExit)
                {
                    foreach (var originFolder in originFolders)
                    {
                        if (!Directory.Exists(originFolder)) continue;
                        var fileNames = Directory.GetFiles(originFolder);
                        if (fileNames != null)
                        {
                            foreach (var fileName in fileNames)
                            {
                                if (!fileName.EndsWith(".plot")) continue;
                                if (!File.Exists(fileName)) continue;

                                var fi = new FileInfo(fileName);
                                if (!fi.Name.StartsWith("plot")) continue;
                                // Detected
                                var size = fi.Length;
                                Console.WriteLine("\n----------------------------------------------------------------------------------------");
                                Console.WriteLine($"{Now()} Detected plot '{fi.Name}' in '{originFolder}' with size {size} bytes.");
                                Console.WriteLine("----------------------------------------------------------------------------------------\n");
                                // Rename
                                string tempName = $"{fi.Name}.apcpm.move";
                                Console.WriteLine($"{Now()} Rename plot. Add suffix '.apcpm.move'");
                                var tempFullName = Path.Combine(originFolder, tempName);
                                File.Move(fi.FullName, tempFullName);
                                var fullTempDestPath = Path.Combine(destinationFolder, tempName);
                                // Move
                                Console.WriteLine($"{Now()} Move to '{destinationFolder}'");
                                var tempFileInfo = new FileInfo(tempFullName);
                                if (File.Exists(fullTempDestPath))
                                {
                                    Console.WriteLine($"{Now()} {fullTempDestPath} already exist, add suffix '.trash'");
                                    File.Move(fullTempDestPath, fullTempDestPath + ".trash", overwrite: true);
                                }
                                Stopwatch sw = new();
                                sw.Start();
                                switch (method)
                                {
                                    case 3:
                                        MoveByMethod3(tempFullName, fullTempDestPath, size, bufferSize);
                                        break;
                                    case 2:
                                        MoveByMethod2(tempFileInfo, fullTempDestPath);
                                        break;
                                    default:
                                    case 1:
                                        MoveByMethod1(tempFullName, fullTempDestPath);
                                        break;
                                }
                                sw.Stop();
                                // Rename to original name
                                Console.WriteLine($"{Now()} Rename plot. Remove suffix '.apcpm.move'");
                                var fullDestPath = Path.Combine(destinationFolder, fi.Name);
                                File.Move(fullTempDestPath, fullDestPath);
                                // Result time
                                Console.WriteLine($"{Now()} 100% plot moved {size} bytes in {sw.ElapsedTicks} ticks ({sw.ElapsedMilliseconds} milliseconds)");
                            }
                        }
                    }
                    Thread.Sleep(5000);
                }
            }
        }

        private static void MoveByMethod1(string tempFullName, string fullTempDestPath)
        {
            File.Move(tempFullName, fullTempDestPath);
        }

        private static void MoveByMethod2(FileInfo tempFileInfo, string fullTempDestPath)
        {
            tempFileInfo.MoveTo(fullTempDestPath);
        }

        private static void MoveByMethod3(string tempFullName, string fullTempDestPath, long originSize, int bufferSize)
        {
            if (bufferSize <= 0)
            {
                bufferSize = 4096;
            }
            using (FileStream stream = File.OpenRead(tempFullName))
            using (FileStream writeStream = File.OpenWrite(fullTempDestPath))
            {
                BinaryReader reader = new(stream);
                BinaryWriter writer = new(writeStream);

                // create a buffer to hold the bytes 
                byte[] buffer = new byte[bufferSize];
                int bytesRead;

                // while the read method returns bytes
                // keep writing them to the output stream
                long part = originSize / 10;
                long nextPart = 0;
                Console.Write($"Progress");
                while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                {
                    writeStream.Write(buffer, 0, bytesRead);
                    if (writeStream.Length >= nextPart)
                    {
                        var percent = (double)writeStream.Length / (double)originSize * 100;
                        Console.Write($"..{Math.Round(percent, 0)}%");
                        nextPart += part;
                    }
                }
                Console.WriteLine("");
            }
            // Check if is correct moved file by size
            var destinationFileInfo = new FileInfo(fullTempDestPath);
            if (destinationFileInfo.Length == originSize)
            {
                File.Delete(tempFullName);
            }
        }

        private static bool GetArgs(string[] args, out List<string> originPaths, out string destinationPath,
            out int method, out int bufferSize)
        {
            bool goExit = false;
            method = 1;
            bufferSize = 4096;
            originPaths = new List<string>();
            destinationPath = null;
            int numArgs = args == null ? 0 : args.Length;
            if (args != null && numArgs % 2 == 0)
            {
                for (int i = 0; !goExit && i < args.Length; i += 2)
                {
                    string arg = args[i];
                    string arg2 = args[i + 1];
                    switch (arg.ToUpper())
                    {
                        case "/O":
                            {
                                string originPath = arg2;
                                if (Directory.Exists(originPath))
                                {
                                    Console.WriteLine($"Origin {originPath} OK!");
                                }
                                else
                                {
                                    Console.WriteLine($"Origin directory not exist: {originPath}");
                                    goExit = true;
                                }
                                originPaths.Add(originPath);
                            }
                            break;
                        case "/D":
                            {
                                destinationPath = arg2;
                                if (Directory.Exists(destinationPath))
                                {
                                    Console.WriteLine($"Destination {destinationPath} OK!");
                                }
                                else
                                {
                                    Console.WriteLine($"Destination directory not exist: {destinationPath}");
                                    goExit = true;
                                }
                            }
                            break;
                        case "/M":
                            {
                                if (int.TryParse(arg2, out int met) && (met == 1 || met == 2 || met == 3))
                                {
                                    Console.WriteLine($"Method of move is {met} OK!");
                                    method = met;
                                }
                                else
                                {
                                    Console.WriteLine($"Method is not int32 value '{arg2}' or is not valid value (1, 2 or 3)");
                                    goExit = true;
                                }
                            }
                            break;
                        case "/B":
                            {
                                if (int.TryParse(arg2, out int buf))
                                {
                                    Console.WriteLine($"Buffer size (only method 3 apply) is {buf} OK!");
                                    bufferSize = buf;
                                }
                                else
                                {
                                    Console.WriteLine($"Buffer size is not int32 value '{arg2}'");
                                    goExit = true;
                                }
                            }
                            break;
                    }
                }
            }
            else
            {
                Console.WriteLine("Number of arguments must be greater than 0 and must be even. Go exit");
                Console.WriteLine($"Num args: {numArgs}");
                for (int i = 0; i < numArgs; i++)
                {
                    string arg = args[i];
                    Console.WriteLine($"Arg #{i}: {arg}");
                }
                goExit = true;
            }
            return goExit;
        }

        private static string Now()
        {
            return DateTime.Now.ToString(_formatDate);
        }

        private static void ShowInfo()
        {
            Console.WriteLine("\n----------------------------------------------------------------------------------------");
            Console.WriteLine("/O - origin path of plots generated by chia.exe");
            Console.WriteLine("\tExample: /O \"C:\\Gen my plots folder\"");
            Console.WriteLine("/D - destination path where we want put plots generated by chia.exe");
            Console.WriteLine("\tExample: /D \"D:\\Chia farm plots folder\"");
            Console.WriteLine("/M - Method of move files (1, 2, 3)");
            Console.WriteLine("\tExample: /M 1");
            Console.WriteLine("/B - Buffer size for method 3 of move files (Default 1024 bytes)");
            Console.WriteLine("\tExample: /B 1024");
            Console.WriteLine("CTRL+C - stop all and exit");
            Console.WriteLine("----------------------------------------------------------------------------------------\n");
        }
    }
}

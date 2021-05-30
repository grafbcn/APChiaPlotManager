using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace APChiaPlotManager
{
    class Program
    {
        private const string _formatDate = "dd/MM/yyyy HH:mm:ss";
        private const string _version = "v.1.0.0";
        private const string _suffixM3 = ".apcpm.move.m3";
        private const short _numPartsMethod3 = 100;

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("----------------------------------------------------------------------------------------");
                Console.WriteLine($"{Now()} Start AP Chia Plot Manager ({_version}) by grafbcn");
                Console.WriteLine("----------------------------------------------------------------------------------------");
                bool goExit = GetArgs(args, out List<string> originPaths, out string destinationPath, out int method, out int bufferSize, out int timeoutMsec);
                if (goExit || args.Contains("/H") || args.Contains("-H") || args.Contains("/h") || args.Contains("-h"))
                {
                    ShowInfo();
                }
                else
                {
                    StartThreadManager(originPaths, destinationPath, method, bufferSize, timeoutMsec);
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

        private static void StartThreadManager(List<string> originFolders, string destinationFolder, int method, int bufferSize, int timeoutMsec)
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
                                if (!File.Exists(fileName)) continue;
                                var fi = new FileInfo(fileName);
                                if (!fi.Name.StartsWith("plot")) continue;

                                // Detect type of move
                                if (fileName.EndsWith(".plot"))
                                {
                                    ProcessNormalMove(fi, originFolder, destinationFolder, method, bufferSize, timeoutMsec);
                                }
                                else if (fileName.EndsWith(_suffixM3))
                                {
                                    // Check if in destination exist same file to continue his copy
                                    string destinationFullName = Path.Combine(destinationFolder, fi.Name);
                                    if (File.Exists(destinationFullName))
                                    {
                                        ProcessContinueMoveM3(fileName, originFolder, destinationFullName, destinationFolder, bufferSize, timeoutMsec);
                                    }
                                }
                                else
                                {
                                    // ignore this file, is possible other process use it
                                }
                            }
                        }
                    }
                    Thread.Sleep(5000);
                }
            }
        }

        private static void ProcessContinueMoveM3(string tempFullName, string originFolder, string fullTempDestPath, string destinationFolder,
            int bufferSize, int timeoutMsec)
        {
            // Detected
            FileInfo tempFileInfo = new(tempFullName);
            var originalSize = tempFileInfo.Length;
            FileInfo fd = new(fullTempDestPath);
            var destinationSize = fd.Length;
            var remainingSize = originalSize - destinationSize;
            var remainPercent = Math.Round((double)remainingSize / (double)originalSize * 100, 2);
            Console.WriteLine("\n----------------------------------------------------------------------------------------");
            Console.WriteLine($"{Now()} Detected incomplete moved plot '{tempFileInfo.Name}' in '{originFolder}' with size {originalSize} bytes.");
            Console.WriteLine($"\tDestination plot '{fd.Name}' in '{destinationFolder}' with size {destinationSize} bytes.");
            Console.WriteLine($"\tThe remaining data remains to be moved {remainingSize} bytes ({remainPercent}%).");
            Console.WriteLine("----------------------------------------------------------------------------------------\n");
            // Suffix info
            Console.WriteLine($"{Now()} Plot has suffix '{_suffixM3} (method 3 move)'");
            // Move
            Console.WriteLine($"{Now()} Continue moving to folder '{destinationFolder}'");
            Stopwatch sw = new();
            sw.Start();
            MoveByMethod3(tempFullName, fullTempDestPath, originalSize, bufferSize, timeoutMsec, sizeDestinationFile: destinationSize);
            sw.Stop();
            // Rename to original name
            Console.WriteLine($"{Now()} Rename plot. Remove suffix '{_suffixM3}'");
            var fullDestPath = fullTempDestPath.Substring(0, tempFullName.Length - _suffixM3.Length);
            File.Move(fullTempDestPath, fullDestPath);
            // Result time
            Console.WriteLine($"{Now()} 100% plot moved {originalSize} bytes in {sw.ElapsedTicks} ticks ({sw.ElapsedMilliseconds} milliseconds)");
        }

        private static void ProcessNormalMove(FileInfo fi, string originFolder, string destinationFolder,
            int method, int bufferSize, int timeoutMsec)
        {
            // Detected
            var size = fi.Length;
            Console.WriteLine("\n----------------------------------------------------------------------------------------");
            Console.WriteLine($"{Now()} Detected plot '{fi.Name}' in '{originFolder}' with size {size} bytes.");
            Console.WriteLine("----------------------------------------------------------------------------------------\n");
            // Rename
            string suffix = $".apcpm.move.m{method}";
            string tempName = $"{fi.Name}{suffix}";
            Console.WriteLine($"{Now()} Rename plot. Add suffix '{suffix}'");
            var tempFullName = Path.Combine(originFolder, tempName);
            File.Move(fi.FullName, tempFullName);
            var fullTempDestPath = Path.Combine(destinationFolder, tempName);
            // Move
            Console.WriteLine($"{Now()} Move to '{destinationFolder}'");
            var tempFileInfo = new FileInfo(tempFullName);
            if (File.Exists(fullTempDestPath))
            {
                string suffixTrash = $".trash{DateTime.Now:ddMMyyyyHHmmss}";
                Console.WriteLine($"{Now()} {fullTempDestPath} already exist, add suffix '{suffixTrash}'");
                File.Move(fullTempDestPath, fullTempDestPath + suffixTrash);
            }
            Stopwatch sw = new();
            sw.Start();
            switch (method)
            {
                case 3:
                    MoveByMethod3(tempFullName, fullTempDestPath, size, bufferSize, timeoutMsec);
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
            Console.WriteLine($"{Now()} Rename plot. Remove suffix '{suffix}'");
            var fullDestPath = Path.Combine(destinationFolder, fi.Name);
            File.Move(fullTempDestPath, fullDestPath);
            // Result time
            Console.WriteLine($"{Now()} 100% plot moved {size} bytes in {sw.ElapsedTicks} ticks ({sw.ElapsedMilliseconds} milliseconds)");
        }

        private static void MoveByMethod1(string tempFullName, string fullTempDestPath)
        {
            File.Move(tempFullName, fullTempDestPath);
        }

        private static void MoveByMethod2(FileInfo tempFileInfo, string fullTempDestPath)
        {
            tempFileInfo.MoveTo(fullTempDestPath);
        }

        private static void MoveByMethod3(string tempFullName, string fullTempDestPath, long originSize, int bufferSize,
            int timeoutMsec, long sizeDestinationFile = 0)
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
                long part = originSize / _numPartsMethod3;
                long nextPart = (long)Math.Ceiling((double)writeStream.Length / (double)originSize * _numPartsMethod3) * part;
                Console.Write($"\rProgress..");
                stream.Seek(sizeDestinationFile, SeekOrigin.Begin);
                writeStream.Seek(sizeDestinationFile, SeekOrigin.Begin);
                while ((bytesRead = stream.Read(buffer, offset: 0, bufferSize)) > 0)
                {
                    writeStream.Write(buffer, 0, bytesRead);
                    if (writeStream.Length >= nextPart)
                    {
                        var percent = (double)writeStream.Length / (double)originSize * 100;
                        Console.Write($"\rProgress..{Math.Round(percent, 0):000}%"); // don't remove spaces
                        nextPart += part;
                    }
                    if (timeoutMsec > 0)
                    {
                        Thread.Sleep(timeoutMsec);
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
            out int method, out int bufferSize, out int timeoutMsec)
        {
            bool goExit = false;
            method = 1;
            bufferSize = 4096;
            originPaths = new List<string>();
            destinationPath = null;
            timeoutMsec = 0;
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
                        case "/T":
                            {
                                if (int.TryParse(arg2, out int timeout))
                                {
                                    Console.WriteLine($"Timeout between copy buffer (only method 3 apply) is {timeout} msec. OK!");
                                    timeoutMsec = timeout;
                                }
                                else
                                {
                                    Console.WriteLine($"Timeout is not int32 value '{arg2}'");
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
            Console.WriteLine("/H - Help/information");
            Console.WriteLine("/T - Timeout (msec) for method 3 between write of bytes to destination file (this descrease speed of copy).");
            Console.WriteLine("\tUse it, if you have errors when your move plot from -t to -d folder.");
            Console.WriteLine("\tPrevent error like this: This should be below 5 seconds to minimize risk of losing rewards.");
            Console.WriteLine("CTRL+C - stop all and exit");
            Console.WriteLine("----------------------------------------------------------------------------------------\n");
        }
    }
}

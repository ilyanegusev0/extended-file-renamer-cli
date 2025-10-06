using System.Text;
using System.Text.RegularExpressions;

namespace ExtendedFileRenamerCLI
{
    internal class Program
    {
        private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly char[] _invalidPathChars = Path.GetInvalidPathChars();

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.InputEncoding = Encoding.GetEncoding(1251);
            Console.OutputEncoding = Encoding.GetEncoding(1251);

            while (true)
            {
                Console.Title = "Extended File Renamer v1.0";

                Console.Clear();

                string path = GetPath();
                while (path == null)
                    path = GetPath();

                string[] allFiles = GetFiles(path);
                if (allFiles == null)
                {
                    ShowError("Path doesn't exist.");
                    continue;
                }

                string[] selectedFiles;
                if (allFiles.Length < 1)
                {
                    ShowWarning("No files to rename.");
                    continue;
                }
                else if (allFiles.Length > 1)
                {
                    selectedFiles = SelectFiles(allFiles);
                    while (selectedFiles == null)
                        selectedFiles = SelectFiles(allFiles);
                }
                else
                {
                    selectedFiles = allFiles;
                }

                string format = GetFormat(selectedFiles);
                while (format == null)
                    format = GetFormat(selectedFiles);

                string[] fileForRename = FormatFiles(selectedFiles, format);
                while (fileForRename == null)
                    fileForRename = FormatFiles(selectedFiles, format);

                bool res = RenameFiles(selectedFiles, fileForRename);
                if (res)
                    ShowSuccess("Files successfully renamed.");
                else
                    ShowError("Renaming of files failed.");
            }
        }

        // ---------- REQUIRED FUNCTIONS ----------

        static string GetPath()
        {
            Console.Clear();

            Console.Write(" PATH: ");
            string path = ReadLineEx(ConsoleColor.Yellow);

            if (string.IsNullOrEmpty(path))
            {
                ShowError("Path can't be empty.");
                return null;
            }

            return path;
        }

        static string[] GetFiles(string path)
        {
            Console.Clear();

            string[] files;

            if (File.Exists(path))
                files = new string[] { path };

            else if (Directory.Exists(path))
                files = Directory.GetFiles(path);

            else files = null;

            return files;
        }

        static string[] SelectFiles(string[] files)
        {
            Console.Clear();

            ShowFiles(files);

            WriteLineEx("========== HELP: ==========\n" +
                " * - all files\n" +
                " 1 - unique file\n" +
                " 1-3 - range of files\n" +
                " +3 - first files\n" +
                " -3 - last files\n" +
                "===========================\n", ConsoleColor.Yellow);

            Console.Write(" SELECT FILES (separated by commas): ");
            string selection = ReadLineEx(ConsoleColor.Yellow);

            if (string.IsNullOrEmpty(selection))
            {
                ShowError("Select at least one file.");
                return null;
            }

            List<string> selectedFiles = new List<string>();

            try
            {
                if (selection.Contains("*"))
                {
                    selectedFiles.AddRange(files);
                }
                else
                {
                    foreach (string el in selection.Split(','))
                    {
                        string part = el.Trim();

                        if (Regex.IsMatch(part, @"^\d+$"))
                        {
                            int index = Convert.ToInt32(part) - 1;

                            if (index < 0 || index >= files.Length)
                                throw new IndexOutOfRangeException("Index out of range.");

                            if (!selectedFiles.Contains(files[index]))
                                selectedFiles.Add(files[index]);
                        }
                        else if (Regex.IsMatch(part, @"(\d+)-(\d+)"))
                        {
                            string[] range = part.Split('-');

                            if (range.Length != 2)
                                throw new FormatException("Range must be contain two values.");

                            int start = int.Parse(range[0]) - 1;
                            int end = int.Parse(range[1]) - 1;

                            if (start > end)
                                Swap(ref start, ref end);

                            if (start > end || start < 0 || end >= files.Length)
                                throw new IndexOutOfRangeException("Index is out of range.");

                            for (int i = start; i <= end; i++)
                            {
                                if (!selectedFiles.Contains(files[i]))
                                    selectedFiles.Add(files[i]);
                            }
                        }
                        else if (Regex.IsMatch(part, @"\+(\d+)"))
                        {
                            int index = int.Parse(part.Substring(1));

                            for (int i = 0; i < index; i++)
                            {
                                if (!selectedFiles.Contains(files[i]))
                                    selectedFiles.Add(files[i]);
                            }
                        }
                        else if (Regex.IsMatch(part, @"-(\d+)"))
                        {

                            int index = int.Parse(part.Substring(1));

                            for (int i = files.Length - index; i < files.Length; i++)
                            {
                                if (!selectedFiles.Contains(files[i]))
                                    selectedFiles.Add(files[i]);
                            }
                        }
                        else
                        {
                            throw new FormatException("Unknown error.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return null;
            }

            return selectedFiles.ToArray();
        }

        static string GetFormat(string[] files)
        {
            Console.Clear();

            ShowFiles(files);

            WriteLineEx("========== HELP: ==========\n" +
                " *O* - original name\n" +
                " *E* - original extension\n" +
                " *E{}* - custom extension\n" +
                " *I{}* - incrementor\n" +
                " *D{}* - decrementor\n" +
                "===========================\n", ConsoleColor.Yellow);

            Console.Write(" FORMAT: ");
            string format = ReadLineEx(ConsoleColor.Yellow);

            // проверка на пустые параметры
            if (Regex.IsMatch(format, @"\*I\{\}\*"))
            {
                ShowError("Marker *I{}* must contain an integer value.");
                return null;
            }
            else if (Regex.IsMatch(format, @"\*D\{\}\*"))
            {
                ShowError("Marker *D{}* must contain integer value.");
                return null;
            }

            // проверка на валидные данные
            if (Regex.IsMatch(format, @"\*I\{[^0-9\-]+\}\*"))
            {
                ShowError("Marker *I{N}* must contain only an integer.");
                return null;
            }
            if (Regex.IsMatch(format, @"\*D\{[^0-9\-]+\}\*"))
            {
                ShowError("Marker *D{N}* must contain only an integer.");
                return null;
            }

            string tempFormat = format.Replace("*O*", "").Replace("*E*", "");

            tempFormat = Regex.Replace(tempFormat, @"\*E\{[^\}]*\}\*", "");
            tempFormat = Regex.Replace(tempFormat, @"\*I\{[^\}]*\}\*", "");
            tempFormat = Regex.Replace(tempFormat, @"\*D\{[^\}]*\}\*", "");

            if (string.IsNullOrEmpty(format))
            {
                ShowError("Format can't be empty.");
                return null;
            }
            else if (tempFormat.Any(c => _invalidFileNameChars.Contains(c) || _invalidPathChars.Contains(c)))
            {
                ShowError("Format contains invalid symbols.");
                return null;
            }

            return format;
        }

        static string[] FormatFiles(string[] files, string format)
        {
            Console.Clear();

            WriteLineEx("========== CHANGES: ==========", ConsoleColor.Yellow);

            List<string> filesForRename = new List<string>();

            int fileIndex = 0;
            foreach (string file in files)
            {
                string newFileName = format;

                newFileName = newFileName.Replace("*O*", Path.GetFileNameWithoutExtension(file));

                newFileName = newFileName.Replace("*E*", Path.GetExtension(file));

                newFileName = Regex.Replace(newFileName, @"\*E\{([.a-zA-Z0-9]+)\}\*", match =>
                {
                    string extension = match.Groups[1].Value;
                    return extension.StartsWith(".") ? extension : "." + extension;
                });

                newFileName = Regex.Replace(newFileName, @"\*I\{(-?\d+)\}\*", match =>
                {
                    int incrementValue = int.Parse(match.Groups[1].Value);
                    return (incrementValue + fileIndex).ToString();
                });

                newFileName = Regex.Replace(newFileName, @"\*D\{(-?\d+)\}\*", match =>
                {
                    int decrementValue = int.Parse(match.Groups[1].Value);
                    return (decrementValue - fileIndex).ToString();
                });

                filesForRename.Add(newFileName);

                Console.WriteLine($" {Path.GetFileName(file)} >>> {newFileName}");

                fileIndex++;
            }

            WriteLineEx("==============================\n", ConsoleColor.Yellow);

            Console.Write(" Confirm renaming? (Y/N): ");
            switch (Console.ReadKey().Key)
            {
                case ConsoleKey.Y:
                    return filesForRename.ToArray();
                case ConsoleKey.N:
                    ShowWarning("Renaming cancelled.");
                    return null;
                default:
                    ShowError("Unknown command.");
                    return null;
            }
        }

        static bool RenameFiles(string[] sourceFiles, string[] destinationFiles)
        {
            if (sourceFiles.Length != destinationFiles.Length)
                return false;

            for (int i = 0; i < sourceFiles.Length; i++)
            {
                File.Move(sourceFiles[i], Path.Combine(Path.GetDirectoryName(sourceFiles[i]), destinationFiles[i]));
            }

            return true;
        }

        // ========== EXTRA FUNCTIONS ==========

        // ----- NOTIFICATIONS -----

        static void ShowError(string message)
        {
            Console.Clear();
            WriteLineEx($" ERROR: {message}\n", ConsoleColor.Red);
            Console.Write("Press any key to try again... ");
            Console.ReadKey();
        }

        static void ShowSuccess(string message)
        {
            Console.Clear();
            WriteLineEx($" SUCCESS: {message}\n", ConsoleColor.Green);
            Console.Write("Press any key to try again... ");
            Console.ReadKey();
        }

        static void ShowWarning(string message)
        {
            Console.Clear();
            WriteLineEx($" WARNING: {message}\n", ConsoleColor.Yellow);
            Console.Write("Press any key to try again... ");
            Console.ReadKey();
        }

        // ----- REPEATING FUNCTIONS -----

        static void ShowFiles(string[] files)
        {
            WriteLineEx("========== FILES: ==========", ConsoleColor.Yellow);

            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                Console.WriteLine($" {i + 1}) {fileName}");
            }

            WriteLineEx("============================\n", ConsoleColor.Yellow);
        }

        // ----- EXTENDED FUNCTIONS -----

        static string ReadLineEx(ConsoleColor color)
        {
            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            string value = Console.ReadLine();
            Console.ForegroundColor = prev;
            return value;
        }
        static void WriteEx(string value, ConsoleColor color)
        {
            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(value);
            Console.ForegroundColor = prev;
        }

        static void WriteLineEx(string value, ConsoleColor color)
        {
            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(value);
            Console.ForegroundColor = prev;
        }

        static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        static void Debug(string msg)
        {
            WriteLineEx(msg, ConsoleColor.Cyan);
            Console.ReadKey();
        }
    }
}
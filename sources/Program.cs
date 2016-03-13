using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using NLog;

namespace Remine.Repo {
    internal class Program {
        private static string REPO_URL = string.Empty;

        private static string ROOT_FOLDER = string.Empty;
        private static NLog.Logger _logger = LogManager.GetCurrentClassLogger();
        private static WebClient _client = new WebClient();

        private static void Main(string[] args) {
            try {
                ROOT_FOLDER = Directory.GetCurrentDirectory();

                if (args.Length < 2 ) {
                    Console.WriteLine("usage RemineRepo <url> <repo_folder>");
                    return;
                }

                REPO_URL = args[0].TrimEnd('/');

                ROOT_FOLDER += "\\" + args[1].TrimEnd('\\');

                _logger.Info($"текущий каталог {ROOT_FOLDER}");

                if (!Directory.Exists(ROOT_FOLDER)) {
                    Directory.CreateDirectory(ROOT_FOLDER);
                    _logger.Info($"каталог {ROOT_FOLDER} создан");
                }
                else {
                    _logger.Info($"каталог {ROOT_FOLDER} существует");
                }

                string data = GetHtml("");

                LoadFiles(data);
                LoadFolders(data);
            }
            catch (Exception e) {
                _logger.Fatal("Не удалось выполнить загрузку", e);
            }

            _logger.Info("Загрузка завершена");
        }

        private static string GetHtml(string subUrl) {
            string url = REPO_URL;
            if (!string.IsNullOrEmpty(subUrl)) {
                url += "/" + subUrl;
            }

            string result = string.Empty;

            _logger.Info($"Загрузка html-документа {url}");

            HttpWebRequest request = null;
            HttpWebResponse response = null;

            try {
                request = (HttpWebRequest) WebRequest.Create(url);
                request.ReadWriteTimeout = 10*1000;
                response = (HttpWebResponse) request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK) {
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = null;

                    if (response.CharacterSet == null) {
                        readStream = new StreamReader(receiveStream);
                    }
                    else {
                        readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                    }

                    result = readStream.ReadToEnd();

                    readStream.Close();
                }
            }
            catch (Exception e) {
                _logger.Error($"ошибка при загрузке html-кода документа {url}", e);
            }
            finally {
                if (response != null) response.Close();
            }

            return result;
        }

        private static List<string> GetFiles(string html) {
            List<string> files = new List<string>();

            HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes("//a[contains(@class,'icon-file')]");

            if (nodes != null) {
                foreach (var node in nodes) {
                    var href = node.Attributes["href"].Value;

                    files.Add(href.Substring(href.IndexOf("changes/")).Replace("changes/", ""));
                }
            }

            return files;
        }

        private static List<string> GetFolders(string html) {
            List<string> folders = new List<string>();

            HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes("//a[contains(@class,'icon-folder')]");

            if (nodes != null) {
                foreach (var node in nodes) {
                    var href = System.Web.HttpUtility.UrlDecode(node.Attributes["href"].Value);
                    folders.Add(href.Substring(href.IndexOf("show/")).Replace("show/", ""));
                }
            }

            return folders;
        }

        private static void LoadFiles(string html) {
            foreach (var file in GetFiles(html)) {
                try {
                    var path = System.Web.HttpUtility.UrlDecode(ROOT_FOLDER + "\\" + file.Replace("/", "\\"));
                    if (!File.Exists(path)) {
                        _client.DownloadFile(System.Web.HttpUtility.UrlDecode(REPO_URL + "/" + file), path);
                        _logger.Info($"загружен файл {file}");
                    }
                    else {
                        _logger.Info($"Файл {file} существует - пропуск");
                    }
                }
                catch (Exception e) {
                    _logger.Error($"ошибка при загрузке файла {file}. Файл не загружен", e);
                }
            }
        }

        private static void LoadFolders(string html) {
            foreach (var folder in GetFolders(html)) {
                try {
                    var path = $"{ROOT_FOLDER}\\" + folder.Replace("/", "\\");

                    if (!Directory.Exists(path)) {
                        Directory.CreateDirectory(path);
                    }
                    _logger.Info($"загружен каталог {folder}");

                    var data = GetHtml(folder);
                    LoadFiles(data);
                    LoadFolders(data);
                }
                catch (Exception e) {
                    _logger.Error($"ошибка при загрузке каталога {folder}. Каталог не загружен", e);
                }
            }
        }
    }
}

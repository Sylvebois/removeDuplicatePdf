using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Data;
using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;

namespace removeDuplicatePdf
{
    class Program
    {
        static void Main(string[] args)
        {
            string folder = "";

            while (true)
            {
                Console.WriteLine("Dossier à traiter : ");
                folder = Console.ReadLine()?.ToLower();

                if (!String.IsNullOrEmpty(folder))
                {
                    break;
                }
            }

            try
            {
                DataTable files = ConvertToDatatable(Directory.GetFiles(folder, "*.pdf"));

                Stopwatch chrono = new Stopwatch();
                chrono.Start();

                foreach (DataRow file in files.Rows)
                {
                    string fullFileName = file["name"].ToString();
                    string fileState = file["state"].ToString();
                    string base64File = file["base64"].ToString();

                    //Si le fichier existe et n'a pas encore été traité ...
                    if (File.Exists(fullFileName) && String.IsNullOrEmpty(fileState))
                    {
                        file["state"] = "keep";

                        //On prend le tronc commun du nom de fichier
                        string commonFileName = fullFileName.Substring(0, fullFileName.Length - 10);

                        //Récupère la liste des fichiers dont le nom et la date correspondent
                        DataRow[] fileList = files.Select("name like '" + commonFileName + "%' and name <> '" + fullFileName + "'");

                        foreach (DataRow fileToCompare in fileList)
                        {
                            string fullFileToCompareName = fileToCompare["name"].ToString();
                            string fileToCompareState = fileToCompare["state"].ToString();
                            string base64FileToCompare = fileToCompare["base64"].ToString();

                            //Si le fichier à comparer existe et n'a pas encore été traité ...
                            if (File.Exists(fullFileToCompareName) && String.IsNullOrEmpty(fileToCompareState))
                            {
                                //Création des images et encodage en base64
                                if (String.IsNullOrEmpty(base64File))
                                {
                                    MemoryStream fileStream = new MemoryStream(System.IO.File.ReadAllBytes(fullFileName));
                                    file["base64"] = PDFToImage(fileStream, 96);
                                    fileStream.Close();

                                    base64File = file["base64"].ToString();
                                }

                                if (String.IsNullOrEmpty(base64FileToCompare))
                                {
                                    MemoryStream fileToCompareStream = new MemoryStream(System.IO.File.ReadAllBytes(fullFileToCompareName));
                                    fileToCompare["base64"] = PDFToImage(fileToCompareStream, 96);
                                    fileToCompareStream.Close();

                                    base64FileToCompare = fileToCompare["base64"].ToString();
                                }

                                //Comparaison
                                if (base64File.Equals(base64FileToCompare))
                                {
                                    fileToCompare["state"] = "delete";
                                    Console.WriteLine("A supprimer : " + fullFileToCompareName + " car idem que \r\n" + fullFileName);
                                }
                            }
                        }
                    }
                }

                chrono.Stop();

                Console.WriteLine("Nombre de fichiers à supprimer : " + files.Select("state = 'delete'").Count() + " sur " + files.Rows.Count);
                Console.WriteLine("Temps écoulé pour la recherche : " + chrono.Elapsed + "\r\n");

                string delete = "";

                while (true)
                {
                    Console.WriteLine("Supprimer les fichiers ? (Y/N)");
                    delete = Console.ReadLine()?.ToLower();

                    if (delete == "y" || delete == "n")
                    {
                        break;
                    }
                }

                if (delete == "y")
                {
                    DataRow[] toDel = files.Select("state = 'delete'");

                    Parallel.ForEach(toDel, fileToDel =>
                    {
                        File.Delete(fileToDel["name"].ToString());
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }
        }

        static string PDFToImage(MemoryStream inputMS, int dpi)
        {
            string base64String = "";

            GhostscriptRasterizer rasterizer = new GhostscriptRasterizer();
            GhostscriptVersionInfo version = new GhostscriptVersionInfo(
                new Version(0, 0, 0), @"C:\Program Files\gs\gs9.20\bin\gsdll64.dll",
                string.Empty, GhostscriptLicense.GPL
            );

            rasterizer.Open(inputMS, version, false);

            for (int i = 1; i <= rasterizer.PageCount; i++)
            {
                MemoryStream ms = new MemoryStream();
                Image img = rasterizer.GetPage(dpi, dpi, 1);
                img.Save(ms, ImageFormat.Jpeg);
                ms.Close();

                base64String = Convert.ToBase64String((byte[])ms.ToArray());
            }

            rasterizer.Close();
            return base64String;
        }

        static DataTable ConvertToDatatable(Array list)
        {
            DataTable dt = new DataTable();

            DataColumn name = new DataColumn("name");
            name.DataType = System.Type.GetType("System.String");
            dt.Columns.Add(name);

            DataColumn lastMod = new DataColumn("lastMod");
            lastMod.DataType = System.Type.GetType("System.DateTime");
            dt.Columns.Add(lastMod);

            DataColumn base64 = new DataColumn("base64");
            base64.DataType = System.Type.GetType("System.String");
            dt.Columns.Add(base64);

            DataColumn state = new DataColumn("state");
            state.DataType = System.Type.GetType("System.String");
            dt.Columns.Add(state);

            foreach (string item in list)
            {
                DataRow row = dt.NewRow();

                row["name"] = item;
                row["lastMod"] = File.GetLastWriteTime(item);
                row["base64"] = "";
                row["state"] = "";

                dt.Rows.Add(row);
            }

            return dt;
        }
    }
}
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Skyline_Project_PDFparser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".pdf";
            dlg.Filter = "PDF Files (*.pdf)|*.pdf";

            Nullable<bool> result = dlg.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                tbPath.Text = filename;
            }
        }

        private void btnParse_Click(object sender, RoutedEventArgs e)
        {
            string filePath = tbPath.Text;
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputFolderPath = System.IO.Path.Combine(desktopPath, "example");


            // The regex patterns
            string outerFolder = @"\b(\d+)\s+([a-z][A-Za-z]*)\b";
            string innerFolder = @"(\d+\.\d+)\s+([a-z][A-Za-z]*)\b";
            string generatedClass = @"(\d+\.\d+\.\d+)\s(\w+)";
            //rtbIspis.Document.Blocks.Clear();
            PdfReader reader = new PdfReader(filePath);
            PdfDocument pdfDoc = new PdfDocument(reader);

            // Iterate through all the pages of the PDF
            for (int i = 1; i <= 5; i++)
            {
                ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                string pageContent = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
                //rtbIspis.Document.Blocks.Add(new Paragraph(new Run(pageContent)));

                MatchCollection matches1 = Regex.Matches(pageContent, outerFolder);
                MatchCollection matches2 = Regex.Matches(pageContent, innerFolder);
                //MatchCollection matches3 = Regex.Matches(pageText, generatedClass);
                if (matches1.Count > 0)
                {
                    foreach (Match match in matches1)
                    {

                        string outerFolderPath = System.IO.Path.Combine(outputFolderPath, match.Groups[2].Value);
                        if (!Directory.Exists(outerFolderPath))
                        {
                            Directory.CreateDirectory(outerFolderPath);
                        }
                        foreach (Match match1 in matches2)
                        {
                            var word = match1.Groups[2].Value;
                            if (word == "TypeReference")
                            {
                                string innerFolderPath = System.IO.Path.Combine(outerFolderPath, "Type");
                                if (!Directory.Exists(innerFolderPath))
                                {
                                    Directory.CreateDirectory(innerFolderPath);
                                }
                            }
                            else if (word == "CommandReference")
                            {
                                string innerFolderPath = System.IO.Path.Combine(outerFolderPath, "Command");
                                if (!Directory.Exists(innerFolderPath))
                                {
                                    Directory.CreateDirectory(innerFolderPath);
                                }
                            }

                        }
                    }
                }
            }
            pdfDoc.Close();
            reader.Close();
        }
    }
}



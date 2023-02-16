using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

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
            //Getting the PDF path and creating the base folder
            string filePath = tbPath.Text;
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputFolderPath = System.IO.Path.Combine(desktopPath, "example");


            // The regex patterns
            string outerFolder = @"\b(?!0)(\d+)\s+((?!ipGateway|struct)[a-z][A-Za-z]*)\b"; //Hardcoded exlusions of ipGateway amd struct
            string innerFolder = @"(\d+)\.(\d+)\s+((TypeReference|CommandReference))\b";
            string generatedClass = @"\b(\d+)\.(\d+)\.(\d+)(?:\s+)?((?!.*\.(?:Response|Request)\b).*[a-zA-Z])\b";
            //rtbIspis.Document.Blocks.Clear();
            PdfReader reader = new PdfReader(filePath);
            PdfDocument pdfDoc = new PdfDocument(reader);

            // Iterate through all the pages of the PDF
            string[] pageContent = new string[pdfDoc.GetNumberOfPages()]; // create array with correct size
            ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                pageContent[i - 1] = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy); // store extracted text in array
            }

            //Going through pageContent and making the necessary folders,subfolders and classes 
            for (int i = 1; i < pdfDoc.GetNumberOfPages(); i++)
            {

                //Regex for matching key sentences
                MatchCollection matchesOuter = Regex.Matches(pageContent[i-1], outerFolder);
                MatchCollection matchesInner = Regex.Matches(pageContent[i-1], innerFolder);
                MatchCollection matchesClass = Regex.Matches(pageContent[i-1], generatedClass);
                if (matchesOuter.Count > 0)
                {
                    foreach (Match match in matchesOuter)
                    {
                        //Creating folders
                        string outerFolderPath = System.IO.Path.Combine(outputFolderPath, match.Groups[2].Value);
                        if (!Directory.Exists(outerFolderPath))
                        {
                            Directory.CreateDirectory(outerFolderPath);
                        }
                        foreach (Match match1 in matchesInner)
                        {

                            var word = match1.Groups[3].Value;

                            //If statement to determine do we need to create Command or Type reference folder or both
                            if (word == "TypeReference" && match.Groups[1].Value == match1.Groups[1].Value)
                            {
                                string innerFolderPath = System.IO.Path.Combine(outerFolderPath, match1.Groups[3].Value);
                                if (!Directory.Exists(innerFolderPath))
                                {
                                    Directory.CreateDirectory(innerFolderPath);
                                }



                                foreach (Match match2 in matchesClass)
                                {



                                    if (match1.Groups[1].Value == match2.Groups[1].Value && match1.Groups[2].Value == match2.Groups[2].Value)
                                    {
                                        //Namespace generation
                                        NamespaceDeclarationSyntax ns = SyntaxFactory.NamespaceDeclaration(
                                        SyntaxFactory.IdentifierName(match.Groups[2].Value + ".Type"))
                                        .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>()).NormalizeWhitespace();

                                        //Class declaration
                                        var classModifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                                        ClassDeclarationSyntax cls = SyntaxFactory.ClassDeclaration(match2.Groups[4].Value)
                                        .WithModifiers(classModifiers).NormalizeWhitespace();
                                        ns = ns.AddMembers(cls);



                                        string className = cls.Identifier.ValueText + ".cs";


                                        string classFolderPath = System.IO.Path.Combine(innerFolderPath, className);
                                        if (!File.Exists(classFolderPath))
                                        {
                                            File.WriteAllText(classFolderPath, ns.ToFullString());
                                        }
                                    }
                                }
                            }
                            else if (word == "CommandReference" && match.Groups[1].Value == match1.Groups[1].Value)
                            {
                                string innerFolderPath = System.IO.Path.Combine(outerFolderPath, match1.Groups[3].Value);
                                if (!Directory.Exists(innerFolderPath))
                                {
                                    Directory.CreateDirectory(innerFolderPath);
                                }


 

                                foreach (Match match2 in matchesClass)
                                {


                                    if (match1.Groups[1].Value == match2.Groups[1].Value && match1.Groups[2].Value == match2.Groups[2].Value)
                                    {
                                        string classFolderPath = System.IO.Path.Combine(innerFolderPath, match2.Groups[4].Value);
                                        //Namespace generation
                                        NamespaceDeclarationSyntax ns = SyntaxFactory.NamespaceDeclaration(
                                        SyntaxFactory.IdentifierName(match.Groups[2].Value + ".Command"))
                                        .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>()).NormalizeWhitespace();

                                        //Class declaration
                                        var classModifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword).NormalizeWhitespace(), SyntaxFactory.Token(SyntaxKind.StaticKeyword).NormalizeWhitespace());

                                        // Define the class names
                                        var requestClassName = "Request";
                                        var responseClassName = "Response";

                                        // Create the base types for the classes
                                        var requestBaseType = SyntaxFactory.ParseName("ApiRequest<Request, Response>");
                                        var responseBaseType = SyntaxFactory.ParseName("ApiResultResponse<object>");

                                        // Create the Request class
                                        var requestClass = SyntaxFactory.ClassDeclaration(requestClassName)
                                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                            .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(requestBaseType).NormalizeWhitespace())).NormalizeWhitespace()).NormalizeWhitespace();

                                        // Create the Response class
                                        var responseClass = SyntaxFactory.ClassDeclaration(responseClassName)
                                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                            .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(responseBaseType).NormalizeWhitespace())).NormalizeWhitespace()).NormalizeWhitespace();

                                        // Create constructors for Request class
                                        var requestConstructorWithGuid = SyntaxFactory.ConstructorDeclaration("Request")
                                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                                            .AddParameterListParameters(
                                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("sessionId"))
                                                    .WithType(SyntaxFactory.ParseTypeName("Guid"))
                                            )
                                            .WithInitializer(
                                                SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                                                    .AddArgumentListArguments(
                                                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("sessionId")),
                                                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                                            SyntaxKind.StringLiteralExpression,
                                                            SyntaxFactory.Literal(classFolderPath)
                                                        ))
                                                    )
                                            )
                                            .WithBody(
                                                SyntaxFactory.Block() 
                                            )
                                            .NormalizeWhitespace();


                                        requestClass = requestClass.AddMembers(requestConstructorWithGuid);

                                        // Create constructors for Response class
                                        var jsonConstructor = SyntaxFactory.ConstructorDeclaration("Response")
                                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                                            .AddAttributeLists(
                                                SyntaxFactory.AttributeList(
                                                    SyntaxFactory.SingletonSeparatedList(
                                                        SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("JsonConstructor"))
                                                    )
                                                )
                                            )
                                            .WithBody(SyntaxFactory.Block()) 
                                            .NormalizeWhitespace();

                                        responseClass = responseClass.AddMembers(jsonConstructor);

                                        ClassDeclarationSyntax cls = SyntaxFactory.ClassDeclaration(match2.Groups[4].Value)
                                        .WithModifiers(SyntaxFactory.TokenList(
                                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                          SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                                             .AddMembers(requestClass, responseClass).NormalizeWhitespace();



                                        // Generate the syntax tree
                                        var tree = SyntaxFactory.CompilationUnit()
                                            .AddMembers(cls)
                                            .NormalizeWhitespace();

                                        ns = ns.AddMembers(cls);
                                        string className = cls.Identifier.ValueText + ".cs";

                                        classFolderPath = System.IO.Path.Combine(innerFolderPath, className);
                                        if (!File.Exists(classFolderPath))
                                        {
                                            File.WriteAllText(classFolderPath, ns.ToFullString());
                                        }
                                    }
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



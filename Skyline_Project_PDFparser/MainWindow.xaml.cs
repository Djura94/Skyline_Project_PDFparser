using iText.Commons.Utils;
using iText.Kernel.Geom;
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
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using static iText.Kernel.Pdf.Colorspace.PdfSpecialCs;

namespace Skyline_Project_PDFparser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string filePath = string.Empty;
        PdfReader reader = null;
        PdfDocument pdfDoc = null;
        string[] pageContent = null;
        string startString = string.Empty;
        string endString = string.Empty;
        private bool isEndOfFile = false;
        NamespaceDeclarationSyntax ns = null;
        ClassDeclarationSyntax cls = null;
        string tableType=string.Empty;
        string description= string.Empty;   

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
            filePath = tbPath.Text;
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputFolderPath = System.IO.Path.Combine(desktopPath, "example");


            // The regex patterns
            string outerFolder = @"\b(?!0)(\d+)\s+((?!ipGateway|struct)[a-z][A-Za-z]*)\b"; //Hardcoded exlusions of ipGateway amd struct
            string innerFolder = @"(\d+)\.(\d+)\s+((TypeReference|CommandReference))\b";
            string generatedClass = @"\b(\d+)\.(\d+)\.(\d+)(?:\s+)?(.*[a-zA-Z])\b";

            reader = new PdfReader(filePath);
            pdfDoc = new PdfDocument(reader);

            // Iterate through all the pages of the PDF
            pageContent = new string[pdfDoc.GetNumberOfPages()]; // create array with correct size
            ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();


            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {

                pageContent[i - 1] = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy); // store extracted text in array

            }

            //Going through pageContent and making the necessary folders,subfolders and classes 
            for (int i = 1; i < pdfDoc.GetNumberOfPages(); i++)
            {

                //Regex for matching key sentences
                MatchCollection matchesOuter = Regex.Matches(pageContent[pdfDoc.GetNumberOfPages() - 1], outerFolder);
                MatchCollection matchesInner = Regex.Matches(pageContent[pdfDoc.GetNumberOfPages() - 1], innerFolder);
                MatchCollection matchesClass = Regex.Matches(pageContent[pdfDoc.GetNumberOfPages() - 1], generatedClass);
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
                                         ns = SyntaxFactory.NamespaceDeclaration(
                                        SyntaxFactory.IdentifierName(match.Groups[2].Value + ".Type"))
                                        .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>()).NormalizeWhitespace();



                                        //read current and next chapter, and text between them
                                        startString = match2.Value.ToString();
                                        endString = string.Empty;
                                        bool isFound = false;
                                        foreach (Match item in matchesClass)
                                        {
                                            if (item.Value.ToString() == startString)
                                            {
                                                isFound = true;
                                            }
                                            if (isFound && item.Value.ToString() != startString)
                                            {
                                                endString = item.Value.ToString() + "\n";
                                                string firstChar = endString[0].ToString();
                                                if (!startString.StartsWith(firstChar))
                                                {//check response and request if would be a error here 
                                                    Match nextMatch = matchesOuter.Cast<Match>()
                                                        .SkipWhile(m => m != match)
                                                        .Skip(1)
                                                        .FirstOrDefault();
                                                    endString = nextMatch?.Value;
                                                    if (match == matchesOuter[matchesOuter.Count - 1])
                                                        isEndOfFile = true;
                                                }

                                                isFound = false;
                                            }
                                        }

                                        string className = match2.Groups[4].Value + ".cs";


                                        string classFolderPath = System.IO.Path.Combine(innerFolderPath, className);
                                        if (!File.Exists(classFolderPath))
                                        {
                                            if (!match2.Value.ToString().Contains(".Response") && !match2.Value.ToString().Contains(".Request"))
                                            {
                                                //Method that populates TypeReference files
                                                PopulateType(match2.Groups[4].Value);
                                                File.WriteAllText(classFolderPath, ns.ToFullString());
                                            }
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

                                    //If statement which makes the C# files that are the Command type
                                    if (match1.Groups[1].Value == match2.Groups[1].Value && match1.Groups[2].Value == match2.Groups[2].Value)
                                    {
                                        string classFolderPath = System.IO.Path.Combine(innerFolderPath, match2.Groups[4].Value);

                                        //Namespace generation
                                        NamespaceDeclarationSyntax ns = SyntaxFactory.NamespaceDeclaration(
                                            SyntaxFactory.IdentifierName(match.Groups[2].Value + ".Command"))
                                            .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>())
                                            .NormalizeWhitespace();

                                        //Class declaration
                                        var classModifiers = SyntaxFactory.TokenList(
                                            SyntaxFactory.Token(SyntaxKind.PublicKeyword).NormalizeWhitespace(),
                                            SyntaxFactory.Token(SyntaxKind.StaticKeyword).NormalizeWhitespace());

                                        // Define the class names
                                        var requestClassName = "Request";
                                        var responseClassName = "Response";

                                        // Create the base types for the classes
                                        var requestBaseType = SyntaxFactory.ParseName("ApiRequest<Request, Response>");
                                        var responseBaseType = SyntaxFactory.ParseName("ApiResultResponse<object>");

                                        // Create the Request class
                                        var requestClass = SyntaxFactory.ClassDeclaration(requestClassName)
                                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                            .WithBaseList(SyntaxFactory.BaseList(
                                                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                                                    SyntaxFactory.SimpleBaseType(requestBaseType).NormalizeWhitespace()
                                                )
                                            ).NormalizeWhitespace())
                                            .NormalizeWhitespace();

                                        // Create the Response class
                                        var responseClass = SyntaxFactory.ClassDeclaration(responseClassName)
                                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                            .WithBaseList(SyntaxFactory.BaseList(
                                                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                                                    SyntaxFactory.SimpleBaseType(responseBaseType).NormalizeWhitespace()
                                                )
                                            ).NormalizeWhitespace())
                                            .NormalizeWhitespace();

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
                                            .WithLeadingTrivia(SyntaxFactory.Comment("/// <summary>\r\n\t\t/// Initializes a new instance of the <see cref=\"Request\"/> class.\r\n\t\t/// </summary>\r\n\t\t/// <param name=\"sessionId\">The session identifier.</param>\r"))
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
                                            .WithBody(SyntaxFactory.Block()).WithLeadingTrivia(SyntaxFactory.Comment("/// <summary>\r\n\t\t/// Initializes a new instance of the <see cref=\"Response\"/> class.\r\n\t\t/// </summary>\r"))
                                            .NormalizeWhitespace();

                                        responseClass = responseClass.AddMembers(jsonConstructor);

                                        // Add the Result property to the Response class
                                        var resultProperty = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("object"), "Result")
                                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                                            .AddAccessorListAccessors(
                                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)))
                                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                                            ).NormalizeWhitespace().WithLeadingTrivia(SyntaxFactory.Comment("/// <summary>\r\n\t\t/// Gets the result\r\n\t\t/// </summary>\r"));


                                        responseClass = responseClass.AddMembers(resultProperty);

                                        ClassDeclarationSyntax cls = SyntaxFactory.ClassDeclaration(match2.Groups[4].Value)
                                            .WithModifiers(classModifiers)
                                            .AddMembers(requestClass, responseClass)
                                            .NormalizeWhitespace();

                                        // Generate the syntax tree
                                        var tree = SyntaxFactory.CompilationUnit()
                                            .AddMembers(ns)
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

        private void PopulateType(string ime)
        {

            startString = startString + "\n";

            int startIndex = pageContent[pdfDoc.GetNumberOfPages() - 1].IndexOf(startString) + startString.Length;
            int endIndex = pageContent[pdfDoc.GetNumberOfPages() - 1].IndexOf(endString, startIndex);

            if (startIndex >= 0 && endIndex >= 0 && !isEndOfFile)
            {
                string extractedText = pageContent[pdfDoc.GetNumberOfPages() - 1].Substring(startIndex, endIndex - startIndex).Trim();

                if (extractedText.Contains("enum\n"))   //Works, but it needs to be polished
                {
                    string searchTerm = "enum\n";
                    int enumIndex = extractedText.IndexOf(searchTerm);

                    if (enumIndex != -1)
                    {
                        string textBeforeEnum = extractedText.Substring(0, enumIndex);
                        string textAfterEnum = extractedText.Substring(enumIndex + searchTerm.Length);

                        Regex regex = new Regex("(•\\s+)(.+)");

                        MatchCollection matches = regex.Matches(textAfterEnum);

                        var documentation = SyntaxFactory.ParseLeadingTrivia(textBeforeEnum);

                        //Create a class
                        cls = SyntaxFactory.ClassDeclaration(ime)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)).NormalizeWhitespace()
                            .NormalizeWhitespace();

                        // Create the enum
                        var optionsEnum = SyntaxFactory.EnumDeclaration("Options")
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .WithLeadingTrivia(documentation).NormalizeWhitespace()
                            .NormalizeWhitespace();

                        // Add the members to the enum
                        foreach (Match match in matches)
                        {
                            var member = SyntaxFactory.EnumMemberDeclaration(match.Groups[2].Value)
                                .WithAttributeLists(SyntaxFactory.SingletonList(
                                    SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("RawValue").NormalizeWhitespace()).NormalizeWhitespace()
                                            .WithArgumentList(SyntaxFactory.AttributeArgumentList(
                                                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                                                    SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                        SyntaxFactory.Literal(match.Groups[2].Value).NormalizeWhitespace()).NormalizeWhitespace()))).NormalizeWhitespace()).NormalizeWhitespace())).NormalizeWhitespace())).NormalizeWhitespace();

                            optionsEnum = optionsEnum.AddMembers(member);
                        }

                        // Add the enum to the class
                        cls = cls.AddMembers(optionsEnum).WithLeadingTrivia(documentation);


                        // Generate the syntax tree
                        var tree = SyntaxFactory.CompilationUnit()
                            .AddMembers(cls)
                            .NormalizeWhitespace();

                        ns = ns.AddMembers(cls);


                    }
                }
                else if (extractedText.Contains("struct\n"))
                {
                    string searchTerm = "struct\n";
                    int structIndex = extractedText.IndexOf(searchTerm);

                    if (structIndex != -1)
                    {
                        string textBeforeStruct = extractedText.Substring(0, structIndex);
                        string textAfterStruct = extractedText.Substring(structIndex + searchTerm.Length);

                        string[] dataArr = textAfterStruct.Split('\n');
                        var result = new List<Dictionary<string, string>>();
                        for (int i = 0; i < dataArr.Length; i += 3)
                        {
                            string firstString = dataArr[i];
                            string secondString = dataArr[i + 1];
                            string thirdString = dataArr[i + 2];
                            if (firstString == "ipGateway" && string.IsNullOrEmpty(secondString))
                            {
                                continue;
                            }
                            result.Add(new Dictionary<string, string>
                                    {
                                        {"first_string", firstString},
                                        {"second_string", secondString},
                                        {"third_string", thirdString}
                                    });
                        }

                    }
                }
                else if (extractedText.Contains("emptystruct\n"))
                {
                    //
                }
                
                else if (extractedText.Contains("variant\n"))
                {
                    //
                }

                /*else 
                 {
                     //end of document (endIndex = -1)
                     int numPages = pdfDoc.GetNumberOfPages();
                     PdfPage lastPage = pdfDoc.GetLastPage();
                     Rectangle pageSize = lastPage.GetPageSize();
                     startIndex = pageContent[pdfDoc.GetNumberOfPages() - 1].IndexOf(startString) + startString.Length;
                     endIndex = (int)pageSize.GetTop();

                     if (startString.Contains("SeamlessStatus"))
                     {
                         if (startString != string.Empty) { }
                     }

                     string extractedText = pageContent[pdfDoc.GetNumberOfPages() - 1].Substring(startIndex, endIndex - startIndex).Trim();
                 }*/
            }

        }


    }
}




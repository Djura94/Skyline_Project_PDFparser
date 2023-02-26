using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace Skyline_Project_PDFparser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields
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
        string typeFile= string.Empty;
        string docName= string.Empty;
        string docVersion= string.Empty;

        // The regex patterns
        string outerFolder = @"\b(?!0)(\d+)\s+((?!ipGateway|struct)[a-z][A-Za-z]*)\b"; //Hardcoded exlusions of ipGateway amd struct
        string innerFolder = @"(\d+)\.(\d+)\s+((TypeReference|CommandReference))\b";
        string generatedClass = @"\b(\d+)\.(\d+)\.(\d+)\s+(.*[a-zA-Z])\b";
        string revision = @"Revision\s*:\s*(\d+\.\d+)";
        string pattern = @"\d{2}\/\d{2}\/\d{4}\s+\d+";
        string specialCase = @"([A-Za-z0-9]+)\s+([A-Za-z]+)";
        string multilineComment = @"([A-Za-z]+( [A-Za-z]+)+)";
        #endregion

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Events
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            GetPDFfileLocation();
        }
        private void btnParse_Click(object sender, RoutedEventArgs e)
        {
            ParsingPDF();
        }
        #endregion

        #region Methods
        private string ExtractPDFcontent()
        {
            filePath = tbPath.Text;
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputFolderPath = System.IO.Path.Combine(desktopPath, "example");

            reader = new PdfReader(filePath);
            pdfDoc = new PdfDocument(reader);

            // Iterate through all the pages of the PDF
            pageContent = new string[pdfDoc.GetNumberOfPages()]; // create array with correct size
            ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();


            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {

                pageContent[i - 1] = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy); // store extracted text in array

            }

            return outputFolderPath;
        }

        //Getting the document version, that is needed for the namespace
        private string GetDocumentVersion()
        {
            string[] firstPage = pageContent[0].Split('\n');
            for (int i = 0; i < firstPage.Length; i++)
            {
                docName = firstPage[0];
                if (Regex.Match(firstPage[i], revision).Success)
                {
                    docVersion = Regex.Match(firstPage[i], revision).Groups[1].Value;
                    docVersion = docVersion.Replace('.', '_');
                }
            }

            return docVersion;
        }
        private void ParsingPDF()
        {
            //Getting the PDF path and creating the base folder
            string outputFolderPath = ExtractPDFcontent();

            //Regex for matching key sentences
            MatchCollection matchesOuter = Regex.Matches(pageContent[pdfDoc.GetNumberOfPages() - 1], outerFolder);
            MatchCollection matchesInner = Regex.Matches(pageContent[pdfDoc.GetNumberOfPages() - 1], innerFolder);
            MatchCollection matchesClass = Regex.Matches(pageContent[pdfDoc.GetNumberOfPages() - 1], generatedClass);

            //Locating the title of the file and its version
            string docVersion = GetDocumentVersion();


            //Going through pageContent and making the necessary folders,subfolders and classes 
            for (int i = 1; i < pdfDoc.GetNumberOfPages(); i++)
            {
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
                                PopulateTypeReference(outerFolderPath, match1, match, matchesClass, matchesOuter);
                            }
                            else if (word == "CommandReference" && match.Groups[1].Value == match1.Groups[1].Value)
                            {
                                PopulateCommands(outerFolderPath, match1, matchesClass, match);
                            }
                        }
                    }
                }
            }
            pdfDoc.Close();
            reader.Close();
        }      
        private void GetPDFfileLocation()
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

        //Populating Command Reference folders with the correct files
        private void PopulateCommands(string outerFolderPath, Match match1, MatchCollection matchesClass, Match match)
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
                        SyntaxFactory.IdentifierName("AppearTV.X20.Api.Schema." + docName + "." + "V" + docVersion + "." + match.Groups[2].Value + ".Command"))
                        .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>())
                        .NormalizeWhitespace();

                    // Create the using directive syntax node
                    var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Newtonsoft.Json")).NormalizeWhitespace();
                    var usingDirectiveSys = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")).NormalizeWhitespace();


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
                    ns = ns.AddUsings(usingDirectiveSys).NormalizeWhitespace();
                    ns = ns.AddUsings(usingDirective).NormalizeWhitespace();
                    ns = ns.AddMembers(cls).NormalizeWhitespace();
                    string className = cls.Identifier.ValueText + ".cs";


                    classFolderPath = System.IO.Path.Combine(innerFolderPath, className);
                    if (!File.Exists(classFolderPath))
                    {
                        File.WriteAllText(classFolderPath, ns.ToFullString());
                    }
                }
            }
        } 

        //Populating Type Reference folders with the correct files
        private void PopulateTypeReference(string outerFolderPath, Match match1, Match match, MatchCollection matchesClass, MatchCollection matchesOuter) 
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
                    SyntaxFactory.IdentifierName("AppearTV.X20.Api.Schema." + docName + "." + "V" + docVersion + "." + match.Groups[2].Value + ".Type"))
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

                            endString = item.Value.ToString();
                            if (!endString.EndsWith("\n"))
                                endString = endString + "\n";

                            if (startString.Substring(0, 3) != endString.Substring(0, 3))
                            {
                                Match nextMatch = matchesOuter.Cast<Match>()
                                .SkipWhile(m => m != match)
                                .Skip(1)
                                .FirstOrDefault();
                                endString = nextMatch?.Value;
                            }
                            if (item.Value.ToString() == matchesClass[matchesClass.Count - 1].Value.ToString())
                            {
                                int numberOfPages = pdfDoc.GetNumberOfPages();
                                string lastPage = numberOfPages.ToString();
                                string datePattern = @"\d{2}\/\d{2}\/\d{4}\s+" + lastPage;
                                Regex footnote =new Regex(datePattern);
                                Match footnoteMatch = footnote.Match(pageContent[pdfDoc.GetNumberOfPages()-1]);
                                if(footnoteMatch.Success)
                                {
                                    endString = footnoteMatch.Value;
                                }
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
                            PopulateType(match2.Groups[4].Value,matchesClass);
                            File.WriteAllText(classFolderPath, ns.ToFullString());
                        }
                    }

                }
            }
        } 

        //Populating different types of Type Reference files
        private void PopulateType(string name, MatchCollection matchesClass)
        {
            if (startString == matchesClass[matchesClass.Count - 1].Value.ToString()) //If statement for the case when it comes to the population of the last class of the pdf file
            {
                int numberOfPages = pdfDoc.GetNumberOfPages();
                string lastPage = numberOfPages.ToString();
                string datePattern = @"\d{2}\/\d{2}\/\d{4}\s+" + lastPage;
                Regex footnote = new Regex(datePattern);
                Match footnoteMatch = footnote.Match(pageContent[pdfDoc.GetNumberOfPages() - 1]);
                if (footnoteMatch.Success)
                {
                    endString = footnoteMatch.Value;
                }
            }

            if (!startString.EndsWith("\n"))
                startString = startString + "\n";

            int startIndex = pageContent[pdfDoc.GetNumberOfPages() - 1].IndexOf(startString) + startString.Length;
            int endIndex = pageContent[pdfDoc.GetNumberOfPages() - 1].IndexOf(endString, startIndex);

            if (startIndex >= 0 && endIndex >= 0 && !isEndOfFile)
            {
                string extractedText = pageContent[pdfDoc.GetNumberOfPages() - 1].Substring(startIndex, endIndex - startIndex).Trim();

                if (extractedText.Contains("enum\n"))   //Works, but it needs to be polished
                {
                    string searchTerm = "enum\n";
                    PopulateEnum(extractedText, name, searchTerm);
                }
                else if (extractedText.Contains("struct\n"))
                {
                    string searchTerm = "struct\n";

                    PopulateStructOrVariant(extractedText, name, searchTerm);
                }
                else if (extractedText.Contains("emptystruct\n"))
                {
                    //
                }
                else if (extractedText.Contains("variant\n"))
                {
                    string searchTerm = "variant\n";
                    PopulateStructOrVariant(extractedText, name, searchTerm);
                }
            }

        } 

        //Method for populating files that are of struct or variant type
        private void PopulateStructOrVariant(string extractedText, string name, string searchTerm)
        {
            int index = extractedText.IndexOf(searchTerm);

            if (index != -1)
            {

                string textBeforeStruct = extractedText.Substring(0, index);
                string textAfterStruct = extractedText.Substring(index + searchTerm.Length);


                // Create the using directive syntax node
                var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Newtonsoft.Json")).NormalizeWhitespace();

                //Create a class
                cls = SyntaxFactory.ClassDeclaration(name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .WithLeadingTrivia(SyntaxFactory.Comment($"/// <summary>{textBeforeStruct}///</summary>\r"))
                    .NormalizeWhitespace();

                // Create constructors for the given class class
                var jsonConstructor = SyntaxFactory.ConstructorDeclaration(name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                    .AddAttributeLists(
                        SyntaxFactory.AttributeList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("JsonConstructor"))
                            )
                        )
                    )

                    .WithBody(SyntaxFactory.Block()).WithLeadingTrivia(SyntaxFactory.Comment($"/// <summary>\r\n\t\t/// Initializes a new instance of the <see cref=\"{name}\"/> class.\r\n\t\t/// </summary>\r"));


                cls = cls.AddMembers(jsonConstructor).NormalizeWhitespace();


                string[] lines = textAfterStruct.Split('\n');
                string nameFile = string.Empty;
                string typeFile = string.Empty;
                string comment = string.Empty;


                //Logic for going through the given text and extracting names of the properties, their types and remarks
                for (int i = 0; i < lines.Length; i++)
                {

                    nameFile = lines[i];

                    if (nameFile == "ipGateway" || Regex.Match(nameFile, pattern).Success)
                        continue;
                    if (Regex.Match(nameFile, specialCase).Success)
                    {
                        string[] names = nameFile.Split(' ');
                        nameFile = names[0];
                        typeFile = names[1];
                        if (lines.Length == 1)
                        {
                            PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                            break;
                        }
                        PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                        continue;
                    }
                    typeFile = lines[i + 1];
                    if (Regex.Match(lines[i + 2], multilineComment).Success) //Multiline comment
                    {
                        comment = lines[i + 2] + " " + lines[i + 3];
                        if (lines.Length == 4)                                //table with only one element but two lined comment
                        {
                            PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                            break;
                        }
                        i = i + 2;
                    }

                    else if (lines.Length > 3 && lines[i + 2].EndsWith("."))                 //Case for single line comment with dot
                    {
                        comment = lines[i + 2];
                        i++;
                        if (lines.Length == 3)                                          //table with only one element
                        {
                            PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                            break;
                        }
                    }
                    else if (Regex.Match(lines[i + 2], pattern).Success && lines[i + 3] == "ipGateway")      //Page break case
                    {
                        if (Regex.Match(lines[i + 4], multilineComment).Success)
                        {
                            comment = lines[i + 4] + " " + lines[i + 5];
                            i = i + 4;
                        }
                        else
                        {
                            comment = lines[i + 4];
                            i = i + 3;
                        }
                    }
                    else if (lines[i + 2].EndsWith(".") && lines.Length == 3)       //table with one element and one lined comment with dot
                    {
                        comment = lines[i + 2];
                        PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                        break;
                    }
                    else if (!lines[i + 2].EndsWith(".") && lines.Length == 3)          //table with one lined comment without the dot
                    {
                        comment = lines[i + 2];
                        PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                        break;
                    }
                    else if (char.IsUpper(lines[i + 2], 0) && !lines[i + 2].EndsWith("."))
                    {
                        comment = lines[i + 2];
                        if (lines[i + 2] == "Prioritylistofmappingrules-eachrulematchesexactlyonesourcecom-") //Hardcoded unique case in pdf file
                        {
                            comment = lines[i + 2] + " " + lines[i + 3];
                            PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                            break;
                        }
                        i++;
                    }

                    i++;
                    PopulateStructOrVariantClasses(nameFile, typeFile, comment);
                }



                // Generate the syntax tree
                var tree = SyntaxFactory.CompilationUnit().AddMembers(ns)
                    .AddMembers(cls)
                    .NormalizeWhitespace();

                ns = ns.AddUsings(usingDirective).NormalizeWhitespace();
                ns = ns.AddMembers(cls).NormalizeWhitespace();

            }
        }

        //Populating struct or variant classes
        private void PopulateStructOrVariantClasses(string fileName, string fileType, string comment)
        {
            //Converting the string that represents what kind of property it is
            var typeIdentifier = SyntaxFactory.IdentifierName(fileType);


            var property = SyntaxFactory.PropertyDeclaration(typeIdentifier, fileName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAccessorListAccessors(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                )

                .AddAttributeLists(
                    SyntaxFactory.AttributeList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("JsonProperty"))
                                .AddArgumentListArguments(
                                    SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(fileName))),
                                    SyntaxFactory.AttributeArgument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("Required"), SyntaxFactory.IdentifierName("Always")))
                                )
                        )
                    )
                ).WithLeadingTrivia(SyntaxFactory.Comment($"/// <remarks>{comment}</remarks>\r"));


            cls = cls.AddMembers(property);

        }

        //Populating enum type files
        private void PopulateEnum(string extractedText, string name, string searchTerm)
        {
            int enumIndex = extractedText.IndexOf(searchTerm);

            if (enumIndex != -1)
            {
                string textBeforeEnum = extractedText.Substring(0, enumIndex);
                string textAfterEnum = extractedText.Substring(enumIndex + searchTerm.Length);

                Regex regex = new Regex("(•\\s+)(.+)");

                MatchCollection matches = regex.Matches(textAfterEnum);

                // Create the using directive syntax node
                var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Newtonsoft.Json")).NormalizeWhitespace();

                //Create a class
                cls = SyntaxFactory.ClassDeclaration(name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                    .WithLeadingTrivia(SyntaxFactory.Comment($"/// <summary>{textBeforeEnum}\n /// </summary>\r"))
                    .NormalizeWhitespace();

                // Create the enum
                var optionsEnum = SyntaxFactory.EnumDeclaration("Options")
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));



                // Add the members to the enum
                foreach (Match match in matches)
                {
                    var member = SyntaxFactory.EnumMemberDeclaration(match.Groups[2].Value)
                   .WithAttributeLists(SyntaxFactory.SingletonList(
                       SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                           SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("RawValue").NormalizeWhitespace()).NormalizeWhitespace()
                               .NormalizeWhitespace()
                               .WithArgumentList(SyntaxFactory.AttributeArgumentList(
                                   SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                                       SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                           SyntaxFactory.Literal(match.Groups[2].Value).NormalizeWhitespace())
                                       ))
                               )
                               .NormalizeWhitespace()
                           )
                           .NormalizeWhitespace()
                       )
                   ))).NormalizeWhitespace();


                    optionsEnum = optionsEnum.AddMembers(member).NormalizeWhitespace();
                }

                // Add the enum to the class
                cls = cls.AddMembers(optionsEnum);


                // Generate the syntax tree
                var tree = SyntaxFactory.CompilationUnit()
                    .AddMembers(cls)
                    .NormalizeWhitespace();

                ns = ns.AddUsings(usingDirective).NormalizeWhitespace();
                ns = ns.AddMembers(cls).NormalizeWhitespace();
            }
        }

        #endregion

    }
}
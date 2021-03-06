﻿using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Remoting;
using TeamControlium.Utilities;

namespace TeamControlium.Controlium
{
    /// <summary>Provides API for test scripts and libraries.  Encapsulates extensions to Selenium WebDriver and a layer of abstraction that gives a unified and TeamControlium
    /// oriented control/element identification system and device/browser independence.
    /// </summary>
    /// <remarks>
    /// SeleniumDriver uses a number of Configuration options that are set explicitly or within the underlying tool if uses and it supports run configurations.  Options
    /// used by SeleniumDriver all use categorised options(Category, Name);
    /// <list type="bullet">
    /// <item>General, SeleniumServerFolder
    /// <para>Defines the location of the folder containing the Selenium server executables. Used when test script is executing in Local mode.</para>
    /// </item>
    /// <item>General, Device
    /// <para>Defines the Device of the target Selenium server.  Valid devices and browsers can be seen in the respective Device &amp; Browsers enumerations;
    /// <list type="bullet">
    /// <item><see cref="Devices"/></item></list>
    /// </para>
    /// </item>
    /// <item>Selenium, Host
    /// <para>Defines who is hosting the selenium server.  If set to localhost or 127.0.0.1 script will execute in local mode.  If
    /// anything else, script will execute in remote mode and SeleniumDriver will search the configuration for a matching category whose
    /// options will constitute the Desired Capabilities passed to the remote server. </para></item>
    /// <item>Selenium, HostURI
    /// <para>When executing the script in remote mode, SeleniumDriver uses this option to locate the listener of the Selenium Server to be used.</para>
    /// </item>
    /// <item>Selenium, ConnectionTimeout
    /// <para>Maximum time (in Milliseconds) to wait for a reply/response from the Selenium server when executing in remote mode</para>
    /// </item>
    /// <item>Selenium, Timeout
    /// <para>Maximum time (in Milliseconds) to wait for a Selenium element find to be successful</para>
    /// </item>
    /// <item>Selenium, DurationSpinnerTimeout
    /// <para>Maximum time (in Milliseconds) to wait for a visible wait indicator (such as the Duration Spinner) to be removed</para>
    /// </item>
    /// </list>
    /// </remarks>
    public partial class SeleniumDriver : IDisposable
    {
        // CONSTANT FIELDS (Added text delete)
        private readonly string[] SeleniumServerFolder = { "Selenium", "SeleniumServerFolder" };          // Location of the local Selenium Servers (Only required if running locally
        private readonly string[] ConfigTimeout = { "Selenium", "ElementFindTimeout" };                   // Timeout when waiting for Elements to be found (or go invisible) in seconds
        private readonly string[] ConfigPollingInterval = { "Selenium", "PollInterval" };                 // When looping on an event wait, this is the loop interval; trade off between keeping wire traffic down and speed of test
        private readonly string[] ConfigPageLoadTimeout = { "Selenium", "PageLoadTimeout" };              // Timeout waiting for a page to load
        private readonly string[] ConfigPopupWindowTimeout = { "Selenium", "PopupWindowTimeout" };        // Timeout waiting for a popup window to show and render
        private readonly string[] ConfigPopupWindowsInterval = { "Selenium", "PopupWindowPollInterval" }; // When waiting for a popup window to show and render this is the loop interval in polling
        private static readonly string[] ConfigBrowser = { "Selenium", "Browser" };                       // Browser used for the UI endpoint we are testing with.

        private readonly string[] ConfigDevice = { "Selenium", "Device" };                                // Device hosting the UI endpoint we are testing with (If Local, usually Win7)
        private readonly string[] ConfigHost = { "Selenium", "Host" };                                    // Who is hosting the selenium server.  Either localhost (or 127.0.0.1) for locally hosted.  Or a named Cloud provider (IE. BrowserStack or SauceLabs) or Custom.
        private readonly string[] ConfigHostURI = { "Selenium", "HostURI" };                              // If not locally hosted, this is the full URL or IPaddress:Port to access the Selenium Server
        private readonly string[] ConfigConnectionTimeout = { "Selenium", "ConnectionTimeout" };          // Timeout when waiting for a response from the Selenium Server (when remote)
        private readonly string[] SeleniumDebugMode = { "Selenium", "DebugMode" };                        // If yes, Selenium is started in debug mode...
        private readonly string[] SeleniumLogFilename = { "Selenium", "LogFile" };                        // Path and file for Selenium Log file.  Default is the console window

        private readonly double defaultTimeout = 60000; // 1 Minute
        private readonly double defaultPollInterval = 500; // 500mS
        private readonly double defaultPageLoadTimeout = 60000; // 1 Minutes
        private readonly double defaultPopupWindowTimeout = 60000; // 1 Minutes
        private readonly double defaultPopupWindowInterval = 500; // 500mS

        private WebDriverWait elementFindTimings;
        private WebDriverWait popupWindowTimings;
        private TimeSpan pollInterval;
        private TimeSpan pageLoadTimeout;
        private ObjectMappingDetails dummyMapping = new ObjectMappingDetails(@"//", "Top Level of DOM");

        // CONSTRUCTORS
        /// <summary>Instantiates SeleniumDriver</summary>
        public SeleniumDriver()
        {
            ConfigureRun();
        }

        public void Dispose()
        {
            if (WebDriver != null) WebDriver.Close();
            WebDriver = null;
        }

        // ENUMS
        /// <summary>Passes basic user credentials around in plain text format</summary>
        public struct Credentials { public string Username; public string Password; public Credentials(string User, string Pass) { Username = User; Password = Pass; } }

        /// <summary>Types of validation for textual compares in tests</summary>
        public enum ValidationCompareTypes
        {
            /// <summary>
            ///  Actual Text must match the Required text exactly.
            /// </summary>
            Exact,
            /// <summary>
            /// Actual text starts with the Required text. 
            /// </summary>
            StartsWith,
            /// <summary>
            /// Actual text ends with the Required text. 
            /// </summary>
            EndsWith,
            /// <summary>
            /// Actual text contains any instances of the Required text.
            /// </summary>
            Contains,
            /// <summary>
            /// Actual text is not equal to or contains any instances of the Required text.
            /// </summary>
            DoesntContain,
            /// <summary>
            /// Actual text is not equal to Required text.  This does not match if the Actual contains an instance of the Required text.  EG. If IsNot 'hello' would not match on 'hello' but would match on 'hello byebye'.
            /// </summary>
            IsNot
        }

        // PROPERTIES
        /// <summary>
        /// Maximum time to wait when locating an element using the find logic
        /// </summary>
        public TimeSpan FindTimeout { get { return elementFindTimings.Timeout; } }

        /// <summary>
        /// Poll time used when waiting for a FindElement command to match an element
        /// </summary>
        public TimeSpan PollInterval { get { return pollInterval; } }

        /// <summary>
        /// Sets/Gets page load timeout
        /// </summary>
        public TimeSpan PageLoadTimeout
        {
            get { return pageLoadTimeout; }
            set
            {
                pageLoadTimeout = value;
                if (WebDriver != null)
                {
                    Log.LogWriteLine(Log.LogLevels.FrameworkDebug, $"Setting Page Load timeout to {pageLoadTimeout.TotalMilliseconds}mS");
                    WebDriver.Manage().Timeouts().PageLoad = pageLoadTimeout;
                }
            }
        }

        public ObjectMappingDetails MappingDetails
        {
            get
            {
                return dummyMapping;
            }
        }

        /// <summary>
        /// Retruns the Selenium WebDriver instance we are using.
        /// </summary>
        public IWebDriver WebDriver { get; private set; }

        // METHODS
        /// <summary>If Selenium Webdriver has the capabilty (either built in or with our SSRemoteWebdriver class)
        /// of taking a screenshot, do it.  Resulting JPG is saved to the image folder under the test
        /// suite's results folder.  Image file name is the test ID, with a suffix if passed.</summary>
        /// <remarks>
        /// This assumes webDriver implements ITakesScreen.  Implementation is by SSRemoteWebDriver so webDriver must be an instantation
        /// of that class.  If webDriver does not implement ITakesScreenshot an exception is thrown.</remarks>
        /// <param name="fileName">Optional. If populated and Test Data option [Screenshot, Filename] is not set sets the name of the file</param>
        /// <example>Take a screenshot:
        /// <code lang="C#">
        /// // Take screenshot and save to Results folder /images/MyName.jpg or path given by test data ["Screenshot", "Filepath"]
        /// SeleniumDriver.TakeScreenshot("MyName");
        /// </code></example>
        public string TakeScreenshot(string fileName = null)
        {
            string filenameUsed = default(string);
            string filepath = default(string);

            try
            {
                if (WebDriver != null)
                {
                    if (WebDriver is ITakesScreenshot)
                    {
                        try
                        {
                            filenameUsed = Utilities.Detokeniser.Detokenize(Repository.GetItemGlobalOrDefault("Screenshot", "Filename", null));
                        }
                        catch { }
                        try
                        {
                            filepath = Repository.GetItemGlobalOrDefault("Screenshot", "FilePath", null);
                        }
                        catch { }

                        filenameUsed = fileName ?? filenameUsed ?? "Screenshot";


                        //
                        // Ensure filename friendly.  Use passed in filename.  If none passed in use name from settings.  If no settings use string 'Screenshot'
                        //
                        filenameUsed = Utilities.General.CleanStringForFilename(fileName ?? filenameUsed ?? "Screenshot") + ".jpg";

                        // Build fully qualified filename.  Use 
                        filenameUsed =  filepath!=null? Path.Combine(filepath, filenameUsed): Path.Combine(Environment.CurrentDirectory, "Screenshots", filenameUsed);

                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filenameUsed)));
 
                        Screenshot screenshot = ((ITakesScreenshot)WebDriver).GetScreenshot();
                        Log.LogWriteLine(Log.LogLevels.TestInformation, "Screenshot - {0}", filenameUsed);

                        using (MemoryStream imageStream = new MemoryStream(screenshot.AsByteArray))
                        {
                            using (FileStream fileStream = new FileStream(filenameUsed, FileMode.Create))
                            {
                                using (Image screenshotImage = Image.FromStream(imageStream))
                                {
                                    screenshotImage.Save(fileStream, ImageFormat.Jpeg);
                                }
                            }
                        }
                        return filenameUsed;
                    }
                    else
                    {
                        throw new NotImplementedException("webDriver does not implement ITakesScreenshot!  Is it RemoteWebDriver?");
                    }
                }
                else
                {
                    Log.LogWriteLine(Log.LogLevels.TestInformation, "webDriver is null!  Unable to take screenshot.");
                }
            }
            catch (Exception ex)
            {
                Log.LogWriteLine(Log.LogLevels.TestInformation, "Exception saving screenshot [{0}]", filenameUsed ?? "filename null!");
                Log.LogWriteLine(Log.LogLevels.TestInformation, "> {0}", ex);
            }
            return string.Empty;
        }

        /// <summary>Quit and close the WebDriver.</summary>
        /// <param name="CallEndTest">If true, tool's EndTest method is called.</param>
        /// <remarks>
        /// At the end of a test some housekeeping may need to be performed.  This performs those tasks;
        /// <list type="number">
        /// <item>If run configuration has option TakeScreenshot (in Debug category) set true Screenshot taken (no suffix)</item>
        /// <item>Selenium connection closed</item></list>
        /// <para/><para/>
        /// It is highly recommended to place the CloseSelenium method call in a finally clause of a test-global try/catch structure (or AfterScenario) to ensure it will always be called.
        /// </remarks>
        /// <example>
        /// <code lang="C#">
        /// try
        /// {
        ///   // test steps
        /// }
        /// catch (Exception ex)
        /// {
        ///   // Test abort and fatal error handling
        /// }
        /// finally
        /// {
        ///   // Lets end the test and have a cleanup
        ///   SeleniumDriver.CloseDriver(true); 
        /// }</code></example>
        public void CloseDriver()
        {
            bool TakeScreenshotOption = false;
            try
            {
                TakeScreenshotOption = General.IsValueTrue(Repository.GetItemGlobalOrDefault("Debug", "TakeScreenshot","false"));
            }
            catch (Exception ex)
            {
                Log.LogWriteLine(Log.LogLevels.TestInformation, "RunCategory Option [Debug, TakeScreenshot] Exception: {0}", ex);
            }

            if (TakeScreenshotOption)
            {
                Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Debug.TakeScreenshot = {0} - Taking screenshot...", TakeScreenshotOption);
                TakeScreenshot(DateTime.Now.ToString("yy-MM-dd_HH-mm-ssFFF"));
            }
            else
                Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Debug.TakeScreenshot = {0} - NOT Taking screenshot...", TakeScreenshotOption);

            if (WebDriver != null) WebDriver.Quit();
            WebDriver = null;

            TestBrowser = String.Empty;
        }

        public virtual T SetControl<T>(T NewControl) where T : ControlBase
        {
            return ControlBase.SetControl<T>(this, NewControl);
        }

        /// <summary>
        /// Returns the title of the current browser window
        /// </summary>
        public string PageTitle
        {
            get
            {
                try
                {
                    return WebDriver.Title ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Log.LogWriteLine(Log.LogLevels.Error, "Error getting window title: {0}", ex);
                    return string.Empty;
                }
            }
        }


        private WebDriverWait GetPollAndTimeout(WebDriverWait Original, TimeSpan? TimeoutOverride, TimeSpan? PollingIntervalOverride)
        {
            WebDriverWait newWDW = new WebDriverWait(WebDriver, Original.Timeout);
            if (TimeoutOverride != null) newWDW.Timeout = (TimeSpan)TimeoutOverride;
            newWDW.PollingInterval = PollingIntervalOverride == null ? Original.PollingInterval : (TimeSpan)PollingIntervalOverride;
            return newWDW;
        }

        private string DoRunConfigTokenSubstitution(string tokenisedString)
        {
            string returnString = tokenisedString;
            returnString = returnString.Replace("%Browser%", TestBrowser.ToString());
            return returnString;
        }

        private DesiredCapabilities GetCapabilities(string HostName)
        {
            DesiredCapabilities caps = new DesiredCapabilities();


            foreach (KeyValuePair<string, dynamic> capability in Repository.GetCategoryGlobal(HostName))
            {
                Type capabilityType = capability.Value.GetType();
                if (capabilityType == typeof(string))
                {
                    string capValue = DoRunConfigTokenSubstitution(capability.Value);
                    Log.LogWriteLine(Log.LogLevels.TestInformation, "Capabilities: [{0}] [{1}]", capability.Key, capValue);
                    caps.SetCapability(capability.Key, capValue);
                }
                else
                    throw new Exception(string.Format("Capability [{0}.{1}] is a {2}! Must be a string", HostName, capability.Key, capabilityType.Name));
            }
            return caps;
        }

        private void SetupRemoteRun()
        {
            Log.LogWriteLine(Log.LogLevels.FrameworkDebug, "Running Selenium remotely");

            // Do desired capabilities first
            string seleniumHost = Repository.GetItemGlobal(ConfigHost[0], ConfigHost[1]);
            DesiredCapabilities requiredCapabilities = GetCapabilities(seleniumHost);

            // Now get the Selenium Host details
            Uri uri;
            string seleniumHostURI = Repository.GetItemGlobal(ConfigHostURI[0], ConfigHostURI[1]);
            if (!Uri.TryCreate(seleniumHostURI, UriKind.Absolute, out uri)) throw new InvalidHostURI(seleniumHostURI);

            // And connection timeout
            int connectionTimeout = Repository.GetItemGlobal(ConfigConnectionTimeout[0], ConfigConnectionTimeout[1]);

            try
            {
                // And go (We use our own webdriver, inherited from RemoteWebDriver so that we can implement any extra stuff (IE. Screen snapshot)
                WebDriver = new ExtendedRemoteWebDriver(uri, requiredCapabilities, connectionTimeout);
            }
            catch (Exception ex)
            {
                throw new SeleniumWebDriverInitError(uri.AbsoluteUri, requiredCapabilities.ToString(), ex);
            }
        }

        private bool IsLocalRun
        {
            get
            {
                string seleniumHost = Repository.GetItemGlobalOrDefault(ConfigHost[0], ConfigHost[1],"");
                if (string.IsNullOrEmpty(seleniumHost))
                {
                    Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Run Parameter [{0}.{1}] not set.  Default to Local run.", ConfigHost[0], ConfigHost[1]);
                    return true;
                }
                return (seleniumHost.ToLower().Equals("localhost") || seleniumHost.ToLower().StartsWith("127.0.0.1"));
            }
        }

        public TimeSpan PollInterval1 { get => pollInterval; set => pollInterval = value; }

        private string CheckAndPreparSeleniumLogFile(string SeleniumDebugFile)
        {
            string seleniumDebugFile = SeleniumDebugFile;

            if (string.IsNullOrWhiteSpace(seleniumDebugFile))
                return null;
            else
            {
                // If path is relative, make it absolute..
                string seleniumDebugFileFolder = (seleniumDebugFile.StartsWith(".")) ?
                    Path.GetDirectoryName(Path.GetFullPath(Path.GetDirectoryName(seleniumDebugFile))) :
                    Path.GetDirectoryName(seleniumDebugFile);
                // File path/name is passed on CMD line so remove all spaces
                string seleniumDebugFileName = Path.GetFileNameWithoutExtension(seleniumDebugFile).Replace(" ", "");
                string seleniumDebugFileExt = Path.GetExtension(seleniumDebugFile);
                Log.LogWriteLine(Log.LogLevels.FrameworkDebug, $"SeleniumDebugFile: [{seleniumDebugFile}]");
                Log.LogWriteLine(Log.LogLevels.FrameworkDebug, $"seleniumDebugFileFolder: [{seleniumDebugFileFolder}]");
                Log.LogWriteLine(Log.LogLevels.FrameworkDebug, $"seleniumDebugFileName: [{seleniumDebugFileName}]");
                Log.LogWriteLine(Log.LogLevels.FrameworkDebug, $"seleniumDebugFileExt: [{seleniumDebugFileExt}]");

                if (string.IsNullOrWhiteSpace(seleniumDebugFileFolder))
                    seleniumDebugFileFolder = Environment.CurrentDirectory;

                string PathAndFile = "";
                try
                {
                    int TotalPathLength = seleniumDebugFileFolder.Length + seleniumDebugFileName.Length + seleniumDebugFileExt.Length + 2;
                    Log.LogWriteLine(Log.LogLevels.FrameworkDebug, "Selenium Debug File - [{0}]", Path.Combine(seleniumDebugFileFolder, (seleniumDebugFileName + seleniumDebugFileExt)));
                    Log.LogWriteLine(Log.LogLevels.FrameworkDebug, "Selenium Debug File TotalPathLength = {0}", TotalPathLength);
                    if (TotalPathLength > 248)
                    {
                        //
                        // Max path length is 248, so we need to fiddle....
                        //
                        if ((seleniumDebugFileFolder.Length - seleniumDebugFileName.Length - seleniumDebugFileExt.Length - 2) > 248)
                        {
                            // Ok, we cant do it so bomb out.
                            Log.LogWriteLine(Log.LogLevels.FrameworkDebug, "seleniumDebugFileFolder length {0} so cannot fix path length by truncating seleniumDebugFileName", seleniumDebugFileFolder.Length);
                            throw new Exception($"Cannot create screenshot.  Full path [{TotalPathLength}] would have been too long (Max 248 chars)");
                        }
                        else
                        {
                            Log.LogWriteLine(Log.LogLevels.FrameworkDebug, "Reducing path length by truncating seleniumDebugFileName (length currently {0})", seleniumDebugFileName.Length);
                            // Ok, we can do it.  Just truncate the TestID the required length...
                            seleniumDebugFileName = seleniumDebugFileName.Substring(0, seleniumDebugFileName.Length - (TotalPathLength - 248));
                            Log.LogWriteLine(Log.LogLevels.FrameworkDebug, "Reduced to length {0}", seleniumDebugFileName.Length);
                        }
                    }

                    PathAndFile = Path.Combine(seleniumDebugFileFolder, (seleniumDebugFileName + seleniumDebugFileExt));
                    (new FileInfo(PathAndFile)).Directory.Create();
                    //File.Delete(Folder);
                    StreamWriter sw = File.CreateText(PathAndFile);
                    sw.WriteLine("TeamControlium Selenium Debug File");
                    sw.Close();
                    return PathAndFile;
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Error creating Selenium Debug information file ({0}): {1}", PathAndFile, ex.Message), ex.InnerException);
                }
            }
        }

        private void SetupLocalRun()
        {
            Log.LogWriteLine(Log.LogLevels.FrameworkDebug, "Running Selenium locally");
            // Running selenium locally.
            string seleniumFolder = Repository.GetItemGlobalOrDefault(SeleniumServerFolder[0], SeleniumServerFolder[1],".");
            bool seleniumDebugMode = General.IsValueTrue(Repository.GetItemGlobalOrDefault(SeleniumDebugMode[0], SeleniumDebugMode[1],"false"));
            string[] seleniumArguments = Repository.GetItemGlobalOrDefault<string[]>("Selenium", "Arguments", default(string[]));
            string seleniumDebugFile;
            Repository.TryGetItemGlobal(SeleniumLogFilename[0], SeleniumLogFilename[1], out seleniumDebugFile);

            // Check the folder exists and chuck a wobbly if it doesnt...
            if (!Directory.Exists(seleniumFolder)) throw new SeleniumFolderError(seleniumFolder);

            try
            {
                if (IsInternetExplorer)
                {
                    //
                    // See https://code.google.com/p/selenium/issues/detail?id=4403
                    //
                    InternetExplorerOptions IEO = new InternetExplorerOptions();
                    IEO.EnsureCleanSession = true;
                    InternetExplorerDriverService service = InternetExplorerDriverService.CreateDefaultService(seleniumFolder);
                    service.LoggingLevel = seleniumDebugMode ? InternetExplorerDriverLogLevel.Debug : InternetExplorerDriverLogLevel.Info;
                    Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Selenium Server Log Level: {0}", service.LoggingLevel.ToString());

                    string ActualDebugFile = CheckAndPreparSeleniumLogFile(seleniumDebugFile);
                    if (!string.IsNullOrWhiteSpace(ActualDebugFile))
                    {
                        Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Writing Selenium Server Output to: {0}", ActualDebugFile);
                        service.LogFile = ActualDebugFile;
                    }
                    else
                        Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Writing Selenium Server Output to console");

                    // IEO.EnableNativeEvents = false;
                    IEO.AddAdditionalCapability("INTRODUCE_FLAKINESS_BY_IGNORING_SECURITY_DOMAINS", (bool)true);  // Enabling this as part of #ITSD1-1126 - If any issues come back to request
                    Log.LogWriteLine(Log.LogLevels.TestInformation, "IE Browser being used.  Setting INTRODUCE_FLAKINESS_BY_IGNORING_SECURITY_DOMAINS active. #ITSD1-1126");
                    WebDriver = new InternetExplorerDriver(service, IEO);
                }

                if (IsEdge)
                {
                    //throw new NotImplementedException("Edge has not yet been implemented.  Implement it then....");
                    EdgeOptions EO = new EdgeOptions();
                    EdgeDriverService service = EdgeDriverService.CreateDefaultService(seleniumFolder);
                    service.UseVerboseLogging = seleniumDebugMode;
                    Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Selenium Server Log Level: {0}", service.UseVerboseLogging ? "Verbose" : "Not verbose");

                    string ActualDebugFile = CheckAndPreparSeleniumLogFile(seleniumDebugFile);
                    if (!string.IsNullOrWhiteSpace(ActualDebugFile))
                    {
                        Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Writing Selenium Server Output to: {0}", ActualDebugFile);
                        // service.LogFile = ActualDebugFile; -- stuck here.  How to define the log file....
                    }
                    else
                        Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Writing Selenium Server Output to console");


                    EO.PageLoadStrategy = (PageLoadStrategy)EdgePageLoadStrategy.Eager;
                    WebDriver = new EdgeDriver(seleniumFolder,EO);
                    

                }

                if (IsChrome)
                {
                    ChromeOptions options = new ChromeOptions();
                    ChromeDriverService service = ChromeDriverService.CreateDefaultService(seleniumFolder);
                    service.EnableVerboseLogging = seleniumDebugMode;
                    if (seleniumArguments!=default(string[])) options.AddArguments(seleniumArguments);
                    string ActualDebugFile = CheckAndPreparSeleniumLogFile(seleniumDebugFile);
                    if (!string.IsNullOrWhiteSpace(ActualDebugFile))
                    {
                        Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Writing Selenium Server Output to: {0}", ActualDebugFile);
                        service.LogPath = ActualDebugFile;
                    }
                    else
                        Log.LogWriteLine(Log.LogLevels.FrameworkInformation, "Writing Selenium Server Output to console");
                    WebDriver = new ChromeDriver(service, options);
                }

                if (WebDriver == null) throw new SeleniumWebDriverInitError(seleniumFolder, IsInternetExplorer ? "Internet Explorer" : IsChrome ? "Chrome" : IsEdge ? "Edge" : "Undefined!!");

            }
            catch (Exception ex)
            {
                throw new SeleniumWebDriverInitError(seleniumFolder, IsInternetExplorer ? "Internet Explorer" : IsChrome ? "Chrome" : IsEdge ? "Edge" : "Undefined!!", ex);
            }
        }

        private void SetTimeouts()
        {
            elementFindTimings = new WebDriverWait(WebDriver, TimeSpan.FromMilliseconds(Repository.GetItemGlobalOrDefault<double>(ConfigTimeout[0], ConfigTimeout[1], defaultTimeout)));
            elementFindTimings.PollingInterval = TimeSpan.FromMilliseconds(Repository.GetItemGlobalOrDefault(ConfigPollingInterval[0], ConfigPollingInterval[1], defaultPollInterval));

            popupWindowTimings = new WebDriverWait(WebDriver, TimeSpan.FromMilliseconds(Repository.GetItemGlobalOrDefault<double>(ConfigPopupWindowTimeout[0], ConfigPopupWindowTimeout[1], defaultPopupWindowTimeout)));
            popupWindowTimings.PollingInterval = TimeSpan.FromMilliseconds(Repository.GetItemGlobalOrDefault(ConfigPopupWindowsInterval[0], ConfigPopupWindowsInterval[1], defaultPopupWindowInterval));

            PageLoadTimeout = TimeSpan.FromMilliseconds(Repository.GetItemGlobalOrDefault<double>(ConfigPageLoadTimeout[0], ConfigPageLoadTimeout[1], defaultPageLoadTimeout));
        }

        private void ConfigureRun()
        {
            // Set browser if it has not already been done
            if (!TestBrowserHasBeenSet) SetTestBrowser();

            if (IsLocalRun)
            {
                // Setup test run based on being local 
                SetupLocalRun();
            }
            else
            {
                // It is a remote run
                SetupRemoteRun();
            }

            // Set the timeouts
            SetTimeouts();
        }
    }
}
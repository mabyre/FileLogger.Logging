# FileLogger.Logging

Because of "Tripous" who wasn't not my cup of thea then to enhence little things but it should be a fork. 
This is a submodule to provide a File Logging Provider to be used with .NET Microsoft.Extensions.Logging.

# Usage

## Get github submodule into your project

Execute in command line to your project's root:

```cli
git submodule add https://github.com/mabyre/FileLogger.Logging.git
```

## At the start of your project like in Program.cs

```csharp
    var host = Host.CreateDefaultBuilder()
        .ConfigureLogging(logging =>
        {
            //logging.ClearProviders();
            logging.AddFileLogger();
            //logging.AddFileLogger(options => {
            //    options.MaxFileSizeInMB = 5;
            //});
        })
        .Build();
```

## Instanciate ILogger and ILoggerFactory

```csharp
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public MainWindow(ILoggerFactory loggerFactory)
        {
            InitializeComponent();
```

## In your program use ILogger 

```csharp
    public partial class YourUserControl : UserControl
    {
        private ILogger<YourUserControl> logger;

        public YourUserControl(ILoggerFactory loggerFactory)
        {
            InitializeComponent();
            
            // In xaml there is some text in Text so add a NexLine
            textBoxLog.Text += Environment.NewLine;

            logger = loggerFactory.CreateLogger<YourUserControl>();
            logger.LogWarning("My UserControl getting initialized");
        }
```

## In appsettings.json file 

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Trace",
      "Microsoft": "Trace",
      "Microsoft.Hosting.Lifetime": "Trace"
    },
    "File": {
      "LogLevel": "Trace",
      "Folder": ".\\Logs"
    },
```

# Reference
- [GitHub - tbebekis - AspNetCore-CustomLoggingProvider](https://github.com/tbebekis/AspNetCore-CustomLoggingProvider)
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Generator.Generators;
using Generator.Writer;

namespace Generator
{
    public class GeneratedTextTransformation
    {
        private Generators.Generator _generator;
        private Writer.Writer _writer;
        private StringBuilder _fileData;

        public string Test()
        {
            Init();
            ReadSchema();
            _writer.Test();
            return _fileData.ToString();
        }

        private void Init()
        {
            _fileData = new StringBuilder();
            _generator = GeneratorFactory.Create(this);
            _writer = WriterFactory.Create(this);

            try
            {
                _generator.Init();
                _writer.Init();
            }
            catch (Exception x)
            {
                var error = Generators.Generator.FormatError(x);
                _writer.PreHeaderInfo();
                Warning(string.Format("Failed to load provider \"{0}\" - {1}", Settings.ProviderName, error));
                WriteLine(string.Empty);
                WriteLine("// ------------------------------------------------------------------------------------------------");
                WriteLine("// Failed to load provider \"{0}\" - {1}", Settings.ProviderName, error);
                WriteLine("// ------------------------------------------------------------------------------------------------");
                WriteLine(string.Empty);
            }
        }

        private void ReadSchema()
        {
            try
            {
                _generator.LoadTables();

                if (Settings.IncludeConnectionSettingComments)
                    _writer.AppendPreHeaderInfo(_generator.DatabaseDetails());

                _generator.LoadStoredProcs();
            }
            catch (Exception x)
            {
                var error = Generators.Generator.FormatError(x);
                _writer.PreHeaderInfo();
                Warning(string.Format("Failed to read the schema information. Error: {0}", error));
                WriteLine(string.Empty);
            }
        }

        public void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));
        }

        public void WriteLine(string message)
        {
            LogToOutput(message);
        }

        public void Warning(string message)
        {
            LogToOutput(string.Format(CultureInfo.CurrentCulture, "Warning: {0}", message));
        }

        public void Error(string message)
        {
            LogToOutput(string.Format(CultureInfo.CurrentCulture, "Error: {0}", message));
        }

        private void LogToOutput(string message)
        {
            if(Settings.WriteOutputToTraceWindow)
                Trace.WriteLine(message);

            _fileData.AppendLine(message);
        }
    }
}
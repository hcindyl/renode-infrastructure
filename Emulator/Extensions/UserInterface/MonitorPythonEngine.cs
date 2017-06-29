//
// Copyright (c) Antmicro
// Copyright (c) Realtime Embedded
//
// This file is part of the Emul8 project.
// Full license details are defined in the 'LICENSE' file.
//
using System;
using Emul8.Utilities;
using System.IO;
using Emul8.Exceptions;
using System.Linq;
using Emul8.UserInterface.Tokenizer;
using AntShell.Commands;
using System.Text;
using Microsoft.Scripting.Hosting;
using Antmicro.Migrant.Hooks;
using Emul8.Core;

namespace Emul8.UserInterface
{
    public class MonitorPythonEngine : PythonEngine
    {
        private readonly string[] Imports =
        {
            "clr.AddReference('Extensions')",
        };

        public MonitorPythonEngine(Monitor monitor)
        {
            string emul8Path;
            string rootFilePath;
            if(!Misc.TryGetEmul8Directory(out emul8Path) && !Misc.TryGetEmul8Directory(Directory.GetCurrentDirectory(), out emul8Path, out rootFilePath))
            {
                throw new RecoverableException("Could not find emul8 root directory.");
            }

            var imports = Engine.CreateScriptSourceFromString(Aggregate(Imports));
            imports.Execute(Scope);

            var monitorPath = Path.Combine(emul8Path, MonitorPyPath);
            if(File.Exists(monitorPath))
            {
                var script = Engine.CreateScriptSourceFromFile(monitorPath); // standard lib
                script.Compile().Execute(Scope);
                Logging.Logger.Log(Logging.LogLevel.Info, "Loaded monitor commands from: {0}", monitorPath);
            }

            Scope.SetVariable("self", monitor);
            Scope.SetVariable("monitor", monitor);
        }

        [PreSerialization]
        protected void BeforeSerialization()
        {
            throw new NotSupportedException("MonitorPythonEngine should not be serialized!");
        }

        public bool ExecuteBuiltinCommand(Token[] command, ICommandInteraction writer)
        {
            var command_name = ((LiteralToken)command[0]).Value;
            if(!Scope.ContainsVariable("mc_" + command_name))
            {
                return false;
            }

            object comm = Scope.GetVariable("mc_" + command_name); // get a method
            var parameters = command.Skip(1).Select(x => x.GetObjectValue()).ToArray();

            ConfigureOutput(writer);

            try
            {
                var result = Engine.Operations.Invoke(comm, parameters);
                if(result != null && (!(result is bool) || !(bool)result))
                {
                    writer.WriteError(String.Format("Command {0} failed, returning \"{1}\".", command_name, result));
                }
            }
            catch(Exception e)
            {
                throw new RecoverableException(e);
            }
            return true;
        }

        public bool TryExecutePythonScript(string fileName, ICommandInteraction writer)
        {
            var script = Engine.CreateScriptSourceFromFile(fileName);
            ExecutePythonScriptInner(script, writer);
            return true;
        }

        public void ExecutePythonCommand(string command, ICommandInteraction writer)
        {
            try
            {
                var script = Engine.CreateScriptSourceFromString(command);
                ExecutePythonScriptInner(script, writer);
            }
            catch(Microsoft.Scripting.SyntaxErrorException e)
            {
                throw new RecoverableException(String.Format("Line : {0}\n{1}", e.Line, e.Message));
            }
        }

        private void ExecutePythonScriptInner(ScriptSource script, ICommandInteraction writer)
        {
            ConfigureOutput(writer);
            try
            {
                script.Execute(Scope);
            }
            catch(Exception e)
            {
                throw new RecoverableException(e);
            }
        }

        public string[] GetPythonCommands(string prefix = "mc_", bool trimPrefix = true)
        {
            return Scope.GetVariableNames().Where(x => x.StartsWith(prefix ?? string.Empty, StringComparison.Ordinal)).Select(x => x.Substring(trimPrefix ? prefix.Length : 0)).ToArray();
        }

        private void ConfigureOutput(ICommandInteraction writer)
        {
            var streamToEventConverter = new StreamToEventConverter();
            var streamToEventConverterForError = new StreamToEventConverter();
            var utf8WithoutBom = new UTF8Encoding(false);

            var inputStream = writer.GetRawInputStream();
            if(inputStream != null)
            {
                Engine.Runtime.IO.SetInput(inputStream, utf8WithoutBom);
            }
            Engine.Runtime.IO.SetOutput(streamToEventConverter, utf8WithoutBom);
            Engine.Runtime.IO.SetErrorOutput(streamToEventConverterForError, utf8WithoutBom);
            streamToEventConverter.BytesWritten += bytes => writer.Write(utf8WithoutBom.GetString(bytes).Replace("\n", "\r\n"));
            streamToEventConverterForError.BytesWritten += bytes => writer.WriteError(utf8WithoutBom.GetString(bytes).Replace("\n", "\r\n"));
        }

        private const string MonitorPyPath = "./scripts/monitor.py";
    }
}


using System;
using System.IO;
using Microsoft.Win32;

namespace DeskDecor
{
    internal interface IStartupStore
    {
        string Read();
        void Write(string command);
        void Delete();
    }

    internal sealed class StartupRegistration
    {
        private readonly IStartupStore _store;
        private readonly string _command;
        private readonly string _executable;
        private readonly Func<string, bool> _exists;
        public StartupRegistration(string executable) : this(executable, new CurrentUserStartupStore()) { }
        internal StartupRegistration(string executable, IStartupStore store) : this(executable, store, File.Exists) { }
        internal StartupRegistration(string executable, IStartupStore store, Func<string, bool> exists)
        {
            _store = store;
            _executable = executable;
            _exists = exists;
            _command = "\"" + executable + "\"";
        }
        public bool Enabled { get { return string.Equals(_store.Read(), _command, StringComparison.OrdinalIgnoreCase); } }
        public bool DisableIfLocationChanged()
        {
            string registered = _store.Read();
            if (string.IsNullOrEmpty(registered)) return false;
            if (string.Equals(registered, _command, StringComparison.OrdinalIgnoreCase) && _exists(_executable)) return false;
            // Also clean up the old registration when launched from a new path.
            // An existing registration is never silently transferred to this copy.
            _store.Delete();
            return true;
        }
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (!_exists(_executable))
                    throw new IOException("软件位置已改变，请从新位置重新打开软件后再开启自启动。");
                _store.Write(_command);
            }
            else _store.Delete();
        }
    }

    internal sealed class CurrentUserStartupStore : IStartupStore
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "DeskDecor";
        public string Read()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                return key == null ? null : key.GetValue(ValueName) as string;
        }
        public void Write(string command)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        public void Delete()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, true))
                if (key != null) key.DeleteValue(ValueName, false);
        }
    }
}

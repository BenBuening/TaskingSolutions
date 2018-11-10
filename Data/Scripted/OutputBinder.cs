using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace TaskingSolutions.Data.DataAccess
{

    public interface IOutputValueBinder
    {
        void Commit();
    }


    internal class OutputValueBinder : IOutputValueBinder
    {
        private Dictionary<string, object> _bindings;
        public object Target { get; private set; }

        public OutputValueBinder(object target)
        {
            _bindings = new Dictionary<string, object>();
            this.Target = target;
        }

        public void Add(string propertyName, object value)
        {
            _bindings.Add(propertyName, value);
        }

        public void Commit()
        {
            if (this.Target != null)
            {
                Type targetType = this.Target.GetType();
                foreach (var pair in _bindings)
                    targetType.GetProperty(pair.Key).SetValue(this.Target, pair.Value);
            }
        }
    }

}

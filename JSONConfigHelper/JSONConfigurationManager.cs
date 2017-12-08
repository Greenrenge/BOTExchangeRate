using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.ExceptionServices;

namespace GreenUtilities.JSONConfigHelper
{
    //TODO:ควรจะเป็น 1 path ต่อ 1 lock object --> ทำให้เป็น static method write ไม่ได้เพราะมันจะ lock ไม่ได้ หรือไม่งั้น lock ก็จะโยงกันมั่ว
    
    public abstract class ConfigLockCheck
    {
        //synchronously read/write all files by this class
        protected static readonly object lockCheck = new object();
    }
    /// <summary>
    /// Read or Write any object T to JSON file, like a config file by providing Absolute Path for class to read/write.
    /// </summary>
    /// <typeparam name="T">Object Type to write</typeparam>
    public class JSONConfigurationManager<T> : ConfigLockCheck where T:new()
    {
        private T _object;
        private JsonSerializerSettings _setting;
        public JSONConfigurationManager(T obj=default(T),JsonSerializerSettings setting=null)
        {
            _object = obj;
            if (setting != null) _setting = setting;
            else _setting = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
            };
        }
        /// <summary>
        /// Writeout the T to JSON file as absolute path (need to include file ext .json in absolute path)
        /// This method use OpenOrCreate file mode, and wipes out all existing data in JSON file everytime it writes.
        /// </summary>
        /// <param name="absolutePath"> Target json file to writeout</param>
        public void WriteConfig(string absolutePath)
        {
            try
            {
                lock (lockCheck)
                {
                    using (var file = new FileStream(absolutePath, FileMode.OpenOrCreate))
                    {
                        file.SetLength(0);
                        using (var stream = new StreamWriter(file, Encoding.UTF8))
                        {
                            stream.AutoFlush = true;
                            var serializer = JsonSerializer.Create(_setting);
                            serializer.Serialize(stream, _object);
                        }

                    }
                }
            }
            catch
            {
                throw;
            }
        }
        /// <summary>
        /// Read the JSON file in absolute path (need to include file ext .json in absolute path) to T object
        /// This method use Open file mode and will then return out T
        /// </summary>
        /// <param name="absolutePath"> Target json file to be read.</param>
        public T ReadConfig(string absolutePath)
        {
            try
            {
                lock (lockCheck)
                {
                    using (var file = new FileStream(absolutePath, FileMode.Open))
                    {
                        using (var stream = new StreamReader(file,Encoding.UTF8))
                        {

                            var serializer = JsonSerializer.Create(_setting);
                            var result = serializer.Deserialize(stream,typeof(T));
                            return (T)result;
                        }

                    }
                }
            }
            catch 
            {
                throw;
            }
        }

    }
}

using System;
using Newtonsoft.Json;

namespace FluentFaker.Tests
{
    public static class ExtensionsForTesting
    {
        //public static void Dump(this JToken token)
        //{
        //    Console.WriteLine(JsonConvert.SerializeObject(token));
        //}

        public static void Dump(this object obj)
        {
            Console.WriteLine(obj.DumpString());
        }
        public static string DumpString(this object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
    }
}
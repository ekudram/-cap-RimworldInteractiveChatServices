// ExtensionEnvelope.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CAP_ChatInteractive.Extension
{
    public static class ExtensionEnvelope
    {
        public static string Ok(object data = null)
        {
            var o = new JObject
            {
                ["success"] = true,
                ["error"] = null,
                ["data"] = data != null ? JToken.FromObject(data) : JValue.CreateNull()
            };
            return o.ToString(Formatting.None);
        }

        public static string Fail(string error, string message = null)
        {
            var o = new JObject
            {
                ["success"] = false,
                ["error"] = error ?? "Error",
                ["message"] = message ?? error,
                ["data"] = JValue.CreateNull()
            };
            return o.ToString(Formatting.None);
        }

        public static string Ping()
        {
            return Ok(new
            {
                ok = true,
                service = "RICS.TwitchExtension",
                transport = "localHttp",
                version = "r1"
            });
        }
    }
}

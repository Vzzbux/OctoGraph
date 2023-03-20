using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace OctopusVis.VisNetworkModel
{
    public class RootObjectWrapper
    {
        public List<Node> Nodes { get; set; }
        public List<Edge> Edges { get; set; }
        public string ServiceUrl { get; set; }
    }

    public class Node
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("label")]
        public string Label { get; set; }
        [JsonPropertyName("shape")]
        public string Shape { get; set; }
        [JsonPropertyName("color")]
        public string Color { get; set; }
        [JsonPropertyName("group")]
        public string Group { get; set; }
        [JsonPropertyName("title")]
        public string PopupText { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        //[JsonPropertyName("icon")]
        //public Icon Icon { get; set; }
    }

    public class Icon
    {
        public static readonly Icon UserIcon = new()
        {
            Face = "'FontAwesome'",
            Code = @"\uf1ad",
            Size = 50,
            Color = "#f0a30a"
        };

        [JsonPropertyName("face")]
        public string Face { get; set; }
        [JsonPropertyName("code")]
        public string Code { get; set; }
        [JsonPropertyName("size")]
        public int Size { get; set; }
        [JsonPropertyName("color")]
        public string Color { get; set; }
    }

    public class Edge
    {
        [JsonPropertyName("from")]
        public string From { get; set; }
        [JsonPropertyName("to")]
        public string To { get; set; }
        [JsonPropertyName("label")]
        public string Label { get; set; }
    }
}

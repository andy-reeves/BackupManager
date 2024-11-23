using CsvHelper.Configuration.Attributes;

namespace BackupManager.Entities
{
    internal class TdarrTranscodeCancelled
    {
        //  "_id","file","DB","footprintId","hasClosedCaptions","container","scannerReads","ffProbeData","file_size","video_resolution","fileMedium","video_codec_name","audio_codec_name","lastPluginDetails","createdAt","bit_rate","duration","statSync","HealthCheck","TranscodeDecisionMaker","lastHealthCheckDate","holdUntil","lastTranscodeDate","bumped","history","oldSize","newSize","newVsOldRatio","videoStreamIndex","lastUpdate"

        [Name("_id")] public string Id { get; set; }

        [Name("file")] public string File { get; set; }

        //  "_id","file"
        [Name("DB")] public string Db { get; set; } // ,"DB"

        // ,
        [Name("footprintId")] public string footprintId { get; set; } // "footprintId",

        [Name("hasClosedCaptions")] public string hasClosedCaptions { get; set; } // "hasClosedCaptions",

        [Name("container")] public string container { get; set; } // "container

        [Name("scannerReads")] public string scannerReads { get; set; } // ","scannerReads",

        [Name("ffProbeData")] public string ffProbeData { get; set; } // "ffProbeData"

        [Name("file_size")] public string file_size { get; set; } // ,"file_size"

        [Name("video_resolution")] public string video_resolution { get; set; } // ,"video_resolution",

        [Name("fileMedium")] public string fileMedium { get; set; } // "fileMedium"

        [Name("video_codec_name")] public string video_codec_name { get; set; } // ,"video_codec_name",

        [Name("audio_codec_name")] public string audio_codec_name { get; set; } // "audio_codec_name"

        [Name("lastPluginDetails")] public string lastPluginDetails { get; set; } // ,"lastPluginDetails"

        [Name("createdAt")] public string createdAt { get; set; } // ,"createdAt",

        [Name("bit_rate")] public string bit_rate { get; set; } // "bit_rate"

        [Name("duration")] public string duration { get; set; } // ,"duration"

        [Name("statSync")] public string statSync { get; set; } // ,"statSync"

        [Name("HealthCheck")] public string HealthCheck { get; set; } // ,"HealthCheck",

        [Name("TranscodeDecisionMaker")] public string TranscodeDecisionMaker { get; set; } // "TranscodeDecisionMaker"

        [Name("lastHealthCheckDate")] public string lastHealthCheckDate { get; set; } // ,"lastHealthCheckDate"

        [Name("holdUntil")] public string holdUntil { get; set; } // ,"holdUntil",

        [Name("lastTranscodeDate")] public string lastTranscodeDate { get; set; } // "lastTranscodeDate"

        [Name("bumped")] public string bumped { get; set; } // ,"bumped"

        [Name("history")] public string history { get; set; } // ,"history"

        [Name("oldSize")] public string oldSize { get; set; } // ,"oldSize"

        [Name("newSize")] public string newSize { get; set; } // ,"newSize"

        [Name("newVsOldRatio")] public string newVsOldRatio { get; set; } // ,"newVsOldRatio",

        [Name("videoStreamIndex")] public string videoStreamIndex { get; set; } // "videoStreamIndex"

        [Name("lastUpdate")] public string lastUpdate { get; set; } // ,"lastUpdate"
    }
}

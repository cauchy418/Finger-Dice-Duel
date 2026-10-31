UnityEditor.AssetDatabase.Refresh();
var folder="Assets/Resources/Audio/Music";int count=0;
foreach(var path in System.IO.Directory.GetFiles(folder,"*.ogg")){
 var importer=(UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(path.Replace('\\','/'));
 var sample=importer.defaultSampleSettings;sample.preloadAudioData=false;sample.loadType=UnityEngine.AudioClipLoadType.Streaming;sample.compressionFormat=UnityEngine.AudioCompressionFormat.Vorbis;sample.quality=.85f;sample.sampleRateSetting=UnityEditor.AudioSampleRateSetting.PreserveSampleRate;importer.defaultSampleSettings=sample;
 importer.forceToMono=false;importer.loadInBackground=true;importer.SaveAndReimport();count++;
}
return "Configured "+count+" stereo streaming music assets.";

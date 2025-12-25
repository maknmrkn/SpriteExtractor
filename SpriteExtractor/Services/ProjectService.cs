using System;
using System.IO;
using Newtonsoft.Json;
using SpriteExtractor.Models;

namespace SpriteExtractor.Services
{
    public static class ProjectService
    {
        public static void SaveProject(SpriteProject project, string path)
        {
            try
            {
                var json = JsonConvert.SerializeObject(project, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save project: {ex.Message}", ex);
            }
        }
        
        public static SpriteProject LoadProject(string path)
        {
            try
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Project file not found: {path}");
                
                var json = File.ReadAllText(path);
                var project = JsonConvert.DeserializeObject<SpriteProject>(json);
                
                // اطمینان از non-null بودن
                return project ?? throw new Exception("Project file is empty or corrupted");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load project: {ex.Message}", ex);
            }
        }
    }
}
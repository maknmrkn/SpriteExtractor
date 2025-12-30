using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpriteExtractor.Models;

namespace SpriteExtractor.Services
{
    public static class ProjectService
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver()
        };

        public static void SaveProject(SpriteProject project, string path)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // اطمینان از اینکه همه Sprites دارای ID هستند
            foreach (var sprite in project.Sprites ?? new List<SpriteDefinition>())
            {
                if (string.IsNullOrWhiteSpace(sprite.Id))
                {
                    sprite.Id = Guid.NewGuid().ToString("N");
                }
            }

            var json = JsonConvert.SerializeObject(project, JsonSettings);

            var tmp = Path.Combine(dir ?? ".", Path.GetRandomFileName() + ".tmp");
            File.WriteAllText(tmp, json, Encoding.UTF8);

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                Debug.WriteLine($"ProjectService.SaveProject: failed to persist project to '{path}': {ex}");
                throw;
            }
        }

        public static SpriteProject LoadProject(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path))
                return new SpriteProject();

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProjectService.LoadProject: failed to read '{path}': {ex}");
                return new SpriteProject();
            }

            SpriteProject project;
            bool jsonContainsId = json.IndexOf("\"Id\"", StringComparison.OrdinalIgnoreCase) >= 0;
            
            try
            {
                project = JsonConvert.DeserializeObject<SpriteProject>(json, JsonSettings) ?? new SpriteProject();
            }
            catch (JsonException)
            {
                // اگر شکست خورد، JSON قدیمی را parse کنید
                project = ParseLegacyJson(json);
                jsonContainsId = false; // فرمت قدیمی قطعاً Id ندارد
            }

            // اطمینان از وجود Sprites list
            if (project.Sprites == null)
            {
                project.Sprites = new List<SpriteDefinition>();
            }

            var changed = false;

            // اگر JSON اصلی Id نداشت، حتی اگر spriteها Id دارند، باید فایل را بروزرسانی کنیم
            if (!jsonContainsId)
            {
                changed = true;
                Debug.WriteLine($"LoadProject: JSON does not contain Id, migration needed");
            }

            // تولید ID برای Sprites فاقد ID
            foreach (var sprite in project.Sprites)
            {
                if (string.IsNullOrWhiteSpace(sprite.Id))
                {
                    sprite.Id = Guid.NewGuid().ToString("N");
                    changed = true;
                    Debug.WriteLine($"LoadProject: Generated ID {sprite.Id} for sprite '{sprite.Name}'");
                }
            }

            // اگر تغییری ایجاد شد، فایل را ذخیره کنید
            if (changed)
            {
                Debug.WriteLine($"LoadProject: Changes detected, saving migrated project to {path}");
                try
                {
                    SaveProject(project, path);
                    Debug.WriteLine($"LoadProject: Migration saved successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ProjectService.LoadProject: failed to persist migrated project '{path}': {ex}");
                }
            }
            else
            {
                Debug.WriteLine($"LoadProject: No changes needed");
            }

            return project;
        }

        private static SpriteProject ParseLegacyJson(string json)
        {
            var project = new SpriteProject();
            
            try
            {
                var jObject = JObject.Parse(json);
                
                // Parse نام پروژه (هم camelCase هم PascalCase)
                project.Name = (jObject["Name"] ?? jObject["name"])?.ToString() ?? "New Project";
                
                // Parse Sprites
                var spritesToken = jObject["Sprites"] ?? jObject["sprites"];
                if (spritesToken is JArray spritesArray)
                {
                    foreach (JObject spriteObj in spritesArray.OfType<JObject>())
                    {
                        var sprite = new SpriteDefinition();
                        
                        // ID را خالی می‌کنیم تا در LoadProject تولید شود
                        sprite.Id = string.Empty;
                        
                        // Parse نام اسپرایت
                        sprite.Name = (spriteObj["Name"] ?? spriteObj["name"])?.ToString() ?? "Sprite";
                        
                        // Parse Bounds
                        var boundsToken = spriteObj["Bounds"] ?? spriteObj["bounds"];
                        if (boundsToken is JObject boundsObj)
                        {
                            var x = boundsObj["X"] ?? boundsObj["x"];
                            var y = boundsObj["Y"] ?? boundsObj["y"];
                            var width = boundsObj["Width"] ?? boundsObj["width"];
                            var height = boundsObj["Height"] ?? boundsObj["height"];
                            
                            if (x != null && y != null && width != null && height != null)
                            {
                                sprite.Bounds = new Rectangle(
                                    x.Value<int>(),
                                    y.Value<int>(),
                                    width.Value<int>(),
                                    height.Value<int>()
                                );
                            }
                        }
                        
                        // Parse Pivot (اگر وجود دارد)
                        var pivotToken = spriteObj["Pivot"] ?? spriteObj["pivot"];
                        if (pivotToken is JObject pivotObj)
                        {
                            var x = pivotObj["X"] ?? pivotObj["x"];
                            var y = pivotObj["Y"] ?? pivotObj["y"];
                            
                            if (x != null && y != null)
                            {
                                sprite.Pivot = new Point(x.Value<int>(), y.Value<int>());
                            }
                        }
                        
                        project.Sprites.Add(sprite);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProjectService.ParseLegacyJson: failed to parse JSON: {ex}");
            }
            
            return project;
        }
    }
}
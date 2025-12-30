using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SpriteExtractor.Models;
using SpriteExtractor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpriteExtractor.Tests
{
    [TestFixture]
    public class ProjectServiceTests
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver()
        };

        [Test]
        public void SpriteDefinition_Id_IsSerialized()
        {
            // Arrange
            var sprite = new SpriteDefinition 
            { 
                Name = "Test Sprite",
                Bounds = new System.Drawing.Rectangle(10, 20, 100, 150)
            };
            
            var originalId = sprite.Id;
            
            // Act
            var json = JsonConvert.SerializeObject(sprite, JsonSettings);
            
            // Assert - بررسی وجود ID در JSON
            Assert.That(json, Does.Contain(originalId), "ID should be in JSON");
            Assert.That(json, Does.Contain("\"Id\""), "JSON should have 'Id' property");
            
            // Verify deserialization
            var deserialized = JsonConvert.DeserializeObject<SpriteDefinition>(json, JsonSettings);
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Id, Is.EqualTo(originalId), "ID should be preserved after deserialization");
        }

        [Test]
        public void SpriteProject_Sprites_Ids_AreSerialized()
        {
            // Arrange
            var project = new SpriteProject 
            { 
                Name = "Test Project",
                SourceImagePath = "test.png"
            };
            
            project.Sprites.Add(new SpriteDefinition { Name = "Sprite1", Bounds = new System.Drawing.Rectangle(0, 0, 32, 32) });
            project.Sprites.Add(new SpriteDefinition { Name = "Sprite2", Bounds = new System.Drawing.Rectangle(32, 0, 32, 32) });
            
            var sprite1Id = project.Sprites[0].Id;
            var sprite2Id = project.Sprites[1].Id;
            
            // Act
            var json = JsonConvert.SerializeObject(project, JsonSettings);
            
            // Assert
            Assert.That(json, Does.Contain(sprite1Id), "Sprite1 ID should be in JSON");
            Assert.That(json, Does.Contain(sprite2Id), "Sprite2 ID should be in JSON");
            
            // Parse as JObject to verify structure
            var jObject = JObject.Parse(json);
            Assert.That(jObject["Sprites"], Is.Not.Null);
            Assert.That(jObject["Sprites"]?.Count(), Is.EqualTo(2));
            Assert.That(jObject["Sprites"]?[0]?["Id"]?.ToString(), Is.EqualTo(sprite1Id));
            Assert.That(jObject["Sprites"]?[1]?["Id"]?.ToString(), Is.EqualTo(sprite2Id));
        }

        [Test]
        public void SaveLoad_PreservesSpriteIds()
        {
            // Arrange
            var project = new SpriteProject { Name = "Test Project" };
            project.Sprites.Add(new SpriteDefinition { Name = "A", Bounds = new System.Drawing.Rectangle(0, 0, 10, 10) });
            project.Sprites.Add(new SpriteDefinition { Name = "B", Bounds = new System.Drawing.Rectangle(10, 10, 20, 20) });

            var spriteAId = project.Sprites[0].Id;
            var spriteBId = project.Sprites[1].Id;

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            
            try
            {
                // Act - Save
                ProjectService.SaveProject(project, path);
                
                // Verify saved content
                var savedContent = File.ReadAllText(path);
                
                // Assert - Check IDs in saved file
                Assert.That(savedContent, Does.Contain(spriteAId), "Sprite A ID should be in saved file");
                Assert.That(savedContent, Does.Contain(spriteBId), "Sprite B ID should be in saved file");
                
                // Act - Load
                var loaded = ProjectService.LoadProject(path);
                
                // Assert
                Assert.That(loaded.Sprites.Count, Is.EqualTo(project.Sprites.Count), "Sprite count mismatch");
                
                for (int i = 0; i < project.Sprites.Count; i++)
                {
                    Assert.That(loaded.Sprites[i].Id, Is.EqualTo(project.Sprites[i].Id), 
                        $"Id changed for sprite index {i} (Name: {project.Sprites[i].Name})");
                }
            }
            finally
            {
                // Cleanup
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Load_OldFormat_GeneratesMissingIds()
        {
            // Arrange - Old JSON without IDs (PascalCase)
            var oldJson = @"{
                ""Name"": ""old"",
                ""Sprites"": [
                    {
                        ""Name"": ""s1"",
                        ""Bounds"": {
                            ""X"": 0,
                            ""Y"": 0,
                            ""Width"": 10,
                            ""Height"": 10
                        }
                    }
                ]
            }";
            
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, oldJson, Encoding.UTF8);

            try
            {
                // Act - Load (should trigger migration)
                var loaded = ProjectService.LoadProject(path);
                
                // Assert - Migration should have generated IDs
                Assert.That(loaded.Sprites, Is.Not.Null);
                Assert.That(loaded.Sprites.Count, Is.EqualTo(1));
                Assert.That(loaded.Sprites.All(s => !string.IsNullOrWhiteSpace(s.Id)), Is.True, 
                    "All sprites should have IDs after migration");
                
                var generatedId = loaded.Sprites[0].Id;

                // Verify file was updated
                var savedContent = File.ReadAllText(path);
                
                // Check that ID exists in file
                Assert.That(savedContent, Does.Contain(generatedId), 
                    $"ID '{generatedId}' should be in saved file after migration");
                
                // Verify reload preserves the same ID
                var reloaded = ProjectService.LoadProject(path);
                Assert.That(reloaded.Sprites[0].Id, Is.EqualTo(generatedId), 
                    "Reloaded project should have the same ID");
            }
            finally
            {
                // Cleanup
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Load_OldFormat_CamelCase_GeneratesMissingIds()
        {
            // Arrange - Old JSON without IDs (camelCase)
            var oldJson = @"{
                ""name"": ""old"",
                ""sprites"": [
                    {
                        ""name"": ""s1"",
                        ""bounds"": {
                            ""x"": 0,
                            ""y"": 0,
                            ""width"": 10,
                            ""height"": 10
                        }
                    }
                ]
            }";
            
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, oldJson, Encoding.UTF8);

            try
            {
                // Act - Load (should trigger migration)
                var loaded = ProjectService.LoadProject(path);
                
                // Assert
                Assert.That(loaded.Sprites, Is.Not.Null);
                Assert.That(loaded.Sprites.Count, Is.EqualTo(1));
                Assert.That(loaded.Sprites.All(s => !string.IsNullOrWhiteSpace(s.Id)), Is.True);
                
                var generatedId = loaded.Sprites[0].Id;

                // Verify file was updated
                var savedContent = File.ReadAllText(path);
                Assert.That(savedContent, Does.Contain(generatedId),
                    $"ID '{generatedId}' should be in saved file after migration");
                
                // Verify reload
                var reloaded = ProjectService.LoadProject(path);
                Assert.That(reloaded.Sprites[0].Id, Is.EqualTo(generatedId),
                    "Reloaded project should have the same ID");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
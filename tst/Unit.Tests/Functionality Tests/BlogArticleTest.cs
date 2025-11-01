using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Realworlddotnet.Core.Entities;
using Realworlddotnet.Data.Contexts;
using Xunit;

namespace Unit.Tests.Functionality_Tests
{
    public class BlogArticleTest
    {
        private static string CreateTempFolder()
        {
            var path = Path.Combine(Path.GetTempPath(),
                                    "article-img-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        [Fact(DisplayName = "Backend – New Article Picture"), Trait("Number", "2.2")]
        public async Task Stores_absolute_url_and_file_on_disk()
        {
            // -------- arrange DB (in-memory SQLite) -------------------------
            await using var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ConduitContext>()
                .UseSqlite(connection)
                .Options;

            await using (var ctx = new ConduitContext(options))
                await ctx.Database.EnsureCreatedAsync();

            // dummy article + user
            const string username = "alice", email = "alice@test.dev";
            var user = new User { Username = username, Email = email, Password = "irrelevant" };
            
            string slug = "turing-maschine-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var article = new Article("Praxis der Turing-Maschine",
                                      "Beschreibung", "Text-Body")
            {
                Slug = slug,
                Author = user
            };

            var tempRoot = CreateTempFolder();
            var fileName = $"{Guid.NewGuid():N}.jpg";
            var absUrl   = $"http://localhost:8081/api/images/articles/{fileName}";
            var diskPath = Path.Combine(tempRoot, "Images", "Articles");
            Directory.CreateDirectory(diskPath);
            var fullPath = Path.Combine(diskPath, fileName);

            await File.WriteAllBytesAsync(fullPath, new byte[] { 0xFF, 0xD8, 0xFF }); 

            article.Images.Add(new ArticleImage { Url = absUrl });

            await using (var ctx = new ConduitContext(options))
            {
                ctx.Users.Add(user);
                ctx.Articles.Add(article);
                await ctx.SaveChangesAsync();
            }

            await using (var ctx = new ConduitContext(options))
            {
                var stored = await ctx.Articles
                                      .Include(a => a.Images)
                                      .SingleAsync(a => a.Slug == slug);

                stored.Images.Should().ContainSingle()
                              .Which.Url.Should().Be(absUrl);

                File.Exists(fullPath).Should().BeTrue();
            }

            Console.WriteLine("Article Picture Test Successfully Completed!!");
        }
    }
}

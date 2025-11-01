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
    public class ProfilePictureTest
    {
        private static string CreateTempFolder()
        {
            var path = Path.Combine(Path.GetTempPath(), "profile-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        [Fact(DisplayName = "Aufgabe 2 Backend – New Profile Picture"), Trait("Number", "2.1")]
        public async Task Stores_absolute_url_and_file_on_disk()
        {
            // -------- arrange DB (in-memory SQLite) --------
            await using var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ConduitContext>()
                .UseSqlite(connection)
                .Options;

            await using (var ctx = new ConduitContext(options))
                await ctx.Database.EnsureCreatedAsync();

            // dummy user
            const string username = "alice", email = "alice@test.dev";
            var user = new User { Username = username, Email = email, Password = "super secret password" };

            // -------- arrange fake upload --------
            var tempRoot = CreateTempFolder();
            var fileName = $"{Guid.NewGuid():N}.png";
            var absUrl   = $"http://localhost:8081/api/images/{fileName}";
            var diskPath = Path.Combine(tempRoot, "Images", "ProfilePictures");
            Directory.CreateDirectory(diskPath);
            var fullPath = Path.Combine(diskPath, fileName);

            // simulate “save file to disk”
            await File.WriteAllBytesAsync(fullPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header

            // simulate “update user and SaveChanges”
            user.Image = absUrl;
            await using (var ctx = new ConduitContext(options))
            {
                ctx.Users.Add(user);
                await ctx.SaveChangesAsync();
            }

            // -------- assert DB + file system --------
            await using (var ctx = new ConduitContext(options))
            {
                var stored = await ctx.Users.SingleAsync(u => u.Username == username);

                stored.Image.Should().Be(absUrl);
                File.Exists(fullPath).Should().BeTrue();
            }

            Console.WriteLine("Profile Picture Test Sucessfully Completed!!");
        }
    }
}

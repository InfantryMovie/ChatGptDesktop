using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ChatGptDesktop.Model
{
    public class ChatDbContext : DbContext
    {
        public DbSet<ChatResponseDb> ChatMessages { get; set; }
        public DbSet<ChatContextDb> ChatContexts { get; set; }

        readonly string _dbPath;

        public ChatDbContext()
        {
            _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatGptDesktop", "GptDesktop.db");
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)); // Создаём папку, если её нет
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка таблицы ChatMessages
            modelBuilder.Entity<ChatResponseDb>().ToTable("ChatMessages").HasKey(c => c.Id);

            // Связь между сообщениями и профилями
            modelBuilder.Entity<ChatResponseDb>()
                .HasOne(c => c.Profile)
                .WithMany(p => p.ChatMessages)
                .HasForeignKey(c => c.ProfileId);

            // Настройка таблицы ChatContexts
            modelBuilder.Entity<ChatContextDb>().ToTable("ChatContexts").HasKey(c => c.Id);
        }
    }

}

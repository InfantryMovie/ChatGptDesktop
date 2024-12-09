using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatGptDesktop.Model
{
    public class ChatResponseDb
    {
        public int Id { get; set; } // Первичный ключ для EF
        public string ResponseId { get; set; }
        public string Object { get; set; }
        public DateTime Created { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public int ProfileId { get; set; }

        public ChatContextDb Profile { get; set; }
    }
}

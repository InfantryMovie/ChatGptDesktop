using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatGptDesktop.Model
{
    public class ChatContextDb
    {
        // класс для отдельной таблице, для хранения контекста данных от ChatGPT
        public int Id { get; set; } // Первичный ключ для профиля
        public string Name { get; set; } // Название профиля (первые 15 символов из первого ответа)        
        public ICollection<ChatResponseDb> ChatMessages { get; set; } // Навигационное свойство
        public ICollection<UserRequestDb> UserRequests { get; set; }
    }
}

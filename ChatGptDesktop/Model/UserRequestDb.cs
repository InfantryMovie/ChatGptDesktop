using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatGptDesktop.Model
{
    public class UserRequestDb
    {
        public int Id { get; set; } 
        public string UserInput { get; set; } // Запрос пользователя
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Время запроса

        public int ProfileId { get; set; } // Внешний ключ профиля
        public ChatContextDb Profile { get; set; } // Навигационное свойство
    }
}

using System;
using DKMovies.Models.Data.DatabaseModels;

namespace DKMovies.Models.ViewModels
{
    public class UserMessagePreview
    {
        public User User { get; set; }
        public byte[]? LatestMessage { get; set; }
        public DateTime? LatestTime { get; set; }
        public int UnreadCount { get; set; }
    }
}
﻿namespace LibrarySystem.DTO
{
    public class BookResponse
    {
        public Guid bookUid { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Genre { get; set; }
    }
}
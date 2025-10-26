using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

namespace WebApp2.Application.DTOs
{
    public class PaginationRequest
    {
        [FromQuery(Name = "skip")]
        public int Skip { get; set; } = 0;

        [FromQuery(Name = "limit")]
        public int Limit { get; set; } = 20;
    }

    public class PagedResponse<T>
    {
        public List<T>? Data { get; set; }
        public int TotalCount { get; set; }
        public int Skip { get; set; }
        public int Limit { get; set; }
    }
}
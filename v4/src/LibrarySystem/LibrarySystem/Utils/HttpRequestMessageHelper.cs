namespace LibrarySystem.Utils
{
    public static class HttpRequestMessageHelper
    {
        public static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
        {
            // Создаем копию объекта HttpRequestMessage с тем же методом и URI
            HttpRequestMessage clone = new HttpRequestMessage(req.Method, req.RequestUri);

            // Копируем содержимое запроса (через MemoryStream) в клонированный объект
            var ms = new MemoryStream();
            if (req.Content != null)
            {
                await req.Content.CopyToAsync(ms).ConfigureAwait(false);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);

                // Копируем заголовки содержимого
                if (req.Content.Headers != null)
                    foreach (var h in req.Content.Headers)
                        clone.Content.Headers.Add(h.Key, h.Value);
            }

            // Устанавливаем версию запроса
            clone.Version = req.Version;

            // Копируем свойства запроса
            foreach (KeyValuePair<string, object> prop in req.Properties)
                clone.Properties.Add(prop);

            // Копируем заголовки запроса
            foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return clone;
        }
    }

}

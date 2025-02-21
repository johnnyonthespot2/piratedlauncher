using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace PiratedLauncher
{
    internal class Checker
    {
        private static Query query = new Query();
        public static async Task<bool> CheckKey(string key)
        {
            query.Initialize();
            var url = $"https://piratedheat.top/api/checkKey.php?api_key={key}";

            try
            {
                string jsonResponse = await query.FetchDataAsync(url);
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    JObject json = JObject.Parse(jsonResponse);
                    return json["valid"]?.Value<bool>() ?? false;
                }
            }
            catch (AggregateException ex)
            {
                var errorMessage = "Key validation failed:\n";
                foreach (var innerEx in ex.InnerExceptions)
                {
                    errorMessage += $"- {innerEx.Message}\n";
                }
                MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Request error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;
        }
    }
}

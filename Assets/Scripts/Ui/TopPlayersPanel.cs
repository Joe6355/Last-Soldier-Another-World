using UnityEngine;
using TMPro;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

public class TopPlayersPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private TextMeshProUGUI playerEntryPrefab;

    private string ConnectionString => MySQLConnector.GetConnectionString();

    void Start()
    {
        LoadTopPlayers();
    }

    void LoadTopPlayers()
    {
        // Очищаем старые записи
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        string query = @"
            SELECT u.username,
                   IF(r.player_deaths = 0, r.enemy_kills, r.enemy_kills / r.player_deaths) AS rating
            FROM ratings r
            JOIN users u ON r.user_id = u.id
            ORDER BY rating DESC
            LIMIT 15;";

        List<(string username, float rating)> topPlayers = new List<(string, float)>();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string username = reader["username"].ToString();
                            float rating = 0;
                            float.TryParse(reader["rating"].ToString(), out rating);
                            topPlayers.Add((username, rating));
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            Debug.LogError("Ошибка загрузки топ игроков: " + ex.Message);
            CreateEntry("Ошибка загрузки", 0);
            return;
        }

        // Создаем UI-строки
        foreach (var player in topPlayers)
        {
            CreateEntry(player.username, player.rating);
        }
    }

    void CreateEntry(string username, float rating)
    {
        var entry = Instantiate(playerEntryPrefab, contentParent);
        entry.text = $"{username} - Рейтинг: {rating:F2}";
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using MySql.Data.MySqlClient;
using TMPro;
public static class UserData
{
    public static int userId = -1;
    public static string username = "";
}
public class MySQLConnector : MonoBehaviour
{
    [Header("UI Elements (optional)")]
    // Эти поля могут отображать информацию об авторизации/регистрации
    [SerializeField] private TextMeshProUGUI loginInfoText;
    [SerializeField] private TextMeshProUGUI registrationInfoText;

    // Храним идентификатор текущего пользователя
    public int CurrentUserId { get; private set; } = -1;

    // Строка подключения к базе
    private string ConnectionString => GetConnectionString();

    public static string GetConnectionString()
    {
        string configPath = System.IO.Path.Combine(Application.dataPath, "..", "database.local.txt");
        try
        {
            if (!System.IO.File.Exists(configPath))
                throw new System.InvalidOperationException("Не найден database.local.txt. Скопируйте пример и заполните реквизиты БД.");

            string connectionString = System.IO.File.ReadAllText(configPath).Trim();
            if (string.IsNullOrEmpty(connectionString))
                throw new System.InvalidOperationException("Файл database.local.txt пуст. Заполните реквизиты БД.");

            return new MySqlConnectionStringBuilder(connectionString).ConnectionString;
        }
        catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException || ex is System.ArgumentException)
        {
            throw new System.InvalidOperationException("Не удалось прочитать database.local.txt. Проверьте файл и строку подключения.");
        }
    }

    void Awake()
    {
        // Этот объект не будет уничтожаться при загрузке новой сцены
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Можно протестировать подключение к базе
        TestConnection();
    }

    public void TestConnection()
    {
        ClearLoginText();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                LogLogin("Подключение к базе прошло успешно!");

                using (MySqlCommand cmd = new MySqlCommand("SELECT 1", conn))
                {
                    object result = cmd.ExecuteScalar();
                    LogLogin("Тестовый запрос вернул: " + result);
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            LogLoginError("Ошибка подключения: " + ex.Message);
        }
    }

    // ---------------------------
    // 1. Регистрация
    // ---------------------------
    public void RegisterUser(string username, string email, string plainPassword)
    {
        ClearRegistrationText();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Создаем пользователя в таблице users
                string insertUserQuery = @"
                    INSERT INTO users (username, email, password, registration_date)
                    VALUES (@username, @email, @password, NOW());
                ";
                using (MySqlCommand cmd = new MySqlCommand(insertUserQuery, conn))
                {
                    cmd.Parameters.Add("@username", MySqlDbType.VarChar, 50).Value = username;
                    cmd.Parameters.Add("@email", MySqlDbType.VarChar, 100).Value = email;
                    cmd.Parameters.Add("@password", MySqlDbType.VarChar, 255).Value = plainPassword;
                    cmd.ExecuteNonQuery();
                }

                // Получаем id вновь созданного пользователя
                long newUserId = 0;
                using (MySqlCommand cmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn))
                {
                    newUserId = System.Convert.ToInt64(cmd.ExecuteScalar());
                }

                // Создаем запись в таблице ratings с начальными значениями для статистики
                string insertRatingsQuery = @"
                    INSERT INTO ratings (user_id, enemy_kills, player_deaths, boss_kills, elite_kills)
                    VALUES (@user_id, 0, 0, 0, 0);
                ";
                using (MySqlCommand cmd = new MySqlCommand(insertRatingsQuery, conn))
                {
                    cmd.Parameters.Add("@user_id", MySqlDbType.Int32).Value = newUserId;
                    cmd.ExecuteNonQuery();
                }

                LogRegistration($"Пользователь {username} создан. user_id = {newUserId}");
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            LogRegistrationError("Ошибка регистрации: " + ex.Message);
        }
    }

    // ---------------------------
    // 2. Авторизация
    // ---------------------------
    public void LoginUser(string email, string plainPassword)
    {
        if (loginInfoText != null)
            loginInfoText.text = "";

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string query = "SELECT id, username, password FROM users WHERE email = @email LIMIT 1";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string storedPassword = reader["password"].ToString();
                            // Сравниваем пароли как строки (в реальном проекте используйте хэширование)
                            if (storedPassword == plainPassword)
                            {
                                int userId = int.Parse(reader["id"].ToString());
                                string username = reader["username"].ToString();

                                // Сохраняем данные в статическом классе и в локальном поле
                                UserData.userId = userId;
                                UserData.username = username;
                                CurrentUserId = userId;

                                if (loginInfoText != null)
                                    loginInfoText.text = $"Добро пожаловать, {username} (ID: {userId})";

                                // Переходим в следующую сцену (например, статистика)
                                SceneManager.LoadScene(1);
                            }
                            else
                            {
                                if (loginInfoText != null)
                                    loginInfoText.text = "[WARNING] Неверный пароль.";
                            }
                        }
                        else
                        {
                            if (loginInfoText != null)
                                loginInfoText.text = "[WARNING] Пользователь с таким email не найден.";
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            if (loginInfoText != null)
                loginInfoText.text = "[ERROR] Ошибка при авторизации: " + ex.Message;
        }
    }

    // ---------------------------
    // 3. Загрузка статистики
    // ---------------------------
    public bool LoadStats(int userId, out int enemyKills, out int playerDeaths, out int bossKills, out int eliteKills)
    {
        enemyKills = 0;
        playerDeaths = 0;
        bossKills = 0;
        eliteKills = 0;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string query = "SELECT enemy_kills, player_deaths, boss_kills, elite_kills FROM ratings WHERE user_id = @user_id LIMIT 1;";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@user_id", userId);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            enemyKills = int.Parse(reader["enemy_kills"].ToString());
                            playerDeaths = int.Parse(reader["player_deaths"].ToString());
                            bossKills = int.Parse(reader["boss_kills"].ToString());
                            eliteKills = int.Parse(reader["elite_kills"].ToString());
                            return true;
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            Debug.LogError("Ошибка загрузки статистики: " + ex.Message);
        }
        return false;
    }

    // ---------------------------
    // 4. Обновление статистики
    // ---------------------------
    public void UpdateEnemyKills(int userId, int increment)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string query = "UPDATE ratings SET enemy_kills = enemy_kills + @inc WHERE user_id = @user_id;";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@inc", MySqlDbType.Int32).Value = increment;
                    cmd.Parameters.Add("@user_id", MySqlDbType.Int32).Value = userId;
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            Debug.LogError("Ошибка при обновлении убийств: " + ex.Message);
        }
    }

    public void UpdatePlayerDeaths(int userId, int increment)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string query = "UPDATE ratings SET player_deaths = player_deaths + @inc WHERE user_id = @user_id;";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@inc", MySqlDbType.Int32).Value = increment;
                    cmd.Parameters.Add("@user_id", MySqlDbType.Int32).Value = userId;
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            Debug.LogError("Ошибка при обновлении смертей: " + ex.Message);
        }
    }

    public void UpdateBossKills(int userId, int increment)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string query = "UPDATE ratings SET boss_kills = boss_kills + @inc WHERE user_id = @user_id;";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@inc", MySqlDbType.Int32).Value = increment;
                    cmd.Parameters.Add("@user_id", MySqlDbType.Int32).Value = userId;
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            Debug.LogError("Ошибка при обновлении убийств боссов: " + ex.Message);
        }
    }

    public void UpdateEliteKills(int userId, int increment)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string query = "UPDATE ratings SET elite_kills = elite_kills + @inc WHERE user_id = @user_id;";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@inc", MySqlDbType.Int32).Value = increment;
                    cmd.Parameters.Add("@user_id", MySqlDbType.Int32).Value = userId;
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch (System.Exception ex) when (ex is MySqlException || ex is System.InvalidOperationException)
        {
            Debug.LogError("Ошибка при обновлении убийств элитных врагов: " + ex.Message);
        }
    }

    // ---------------------------
    // Вспомогательные методы UI
    // ---------------------------
    private void ClearLoginText()
    {
        if (loginInfoText != null) loginInfoText.text = "";
    }
    private void ClearRegistrationText()
    {
        if (registrationInfoText != null) registrationInfoText.text = "";
    }
    private void LogLogin(string msg)
    {
        if (loginInfoText != null) loginInfoText.text += msg + "\n";
        else Debug.Log(msg);
    }
    private void LogLoginError(string msg)
    {
        if (loginInfoText != null) loginInfoText.text += "[ERROR] " + msg + "\n";
        else Debug.LogError(msg);
    }
    private void LogRegistration(string msg)
    {
        if (registrationInfoText != null) registrationInfoText.text += msg + "\n";
        else Debug.Log(msg);
    }
    private void LogRegistrationError(string msg)
    {
        if (registrationInfoText != null) registrationInfoText.text += "[ERROR] " + msg + "\n";
        else Debug.LogError(msg);
    }
}

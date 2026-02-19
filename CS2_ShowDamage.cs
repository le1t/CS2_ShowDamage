using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json.Serialization;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CS2ShowDamage;

public class ShowDamageConfig : BasePluginConfig
{
    /// <summary>
    /// Включить/выключить плагин
    /// 1 - включено, 0 - выключено
    /// </summary>
    [JsonPropertyName("css_showdamage_enabled")]
    public int Enabled { get; set; } = 1;

    /// <summary>
    /// Длительность отображения уведомления (секунды)
    /// Диапазон: 0.1-10.0
    /// </summary>
    [JsonPropertyName("css_showdamage_duration")]
    public float NotifyDuration { get; set; } = 1.0f;

    /// <summary>
    /// Уровень логирования
    /// 0 - Trace
    /// 1 - Debug
    /// 2 - Information
    /// 3 - Warning
    /// 4 - Error
    /// 5 - Critical
    /// Диапазон: 0-5
    /// </summary>
    [JsonPropertyName("css_showdamage_log_level")]
    public int LogLevel { get; set; } = 4;

    /// <summary>
    /// Режим цвета для урона в HUD
    /// 1 - Динамический цвет в зависимости от урона
    /// Диапазон: 1
    /// </summary>
    [JsonPropertyName("css_showdamage_hud_color_mode")]
    public int HudColorMode { get; set; } = 1;

    /// <summary>
    /// Включить суммарный подсчет урона от гранат и молотов
    /// 1 - включено, 0 - выключено
    /// </summary>
    [JsonPropertyName("css_showdamage_grenade_total_enabled")]
    public int GrenadeTotalEnabled { get; set; } = 1;

    /// <summary>
    /// Длительность отображения суммарного урона от гранат (секунды)
    /// Диапазон: 1.0-10.0
    /// </summary>
    [JsonPropertyName("css_showdamage_grenade_total_duration")]
    public float GrenadeTotalDuration { get; set; } = 3.0f;

    /// <summary>
    /// Длительность агрегации урона от молотового коктейля (секунды)
    /// Диапазон: 1.0-15.0
    /// </summary>
    [JsonPropertyName("css_showdamage_molotov_aggregation_duration")]
    public float MolotovAggregationDuration { get; set; } = 7.0f;

    /// <summary>
    /// Сообщение для суммарного урона от HE-гранаты
    /// {0} - общий урон, {1} - количество пораженных игроков
    /// </summary>
    [JsonPropertyName("css_showdamage_grenade_total_message")]
    public string GrenadeTotalMessage { get; set; } = "Общий урон от гранаты: <font color='red'>{0} HP</font> (поражено: <font color='green'>{1} игроков</font>)";

    /// <summary>
    /// Сообщение для суммарного урона от молотового коктейля
    /// {0} - общий урон, {1} - количество пораженных игроков
    /// </summary>
    [JsonPropertyName("css_showdamage_molotov_total_message")]
    public string MolotovTotalMessage { get; set; } = "Общий урон от молотового: <font color='red'>{0} HP</font> (поражено: <font color='green'>{1} игроков</font>)";

    /// <summary>
    /// Включить общий подсчет урона от пуль за выстрел
    /// 1 - включено, 0 - выключено
    /// </summary>
    [JsonPropertyName("css_showdamage_bullet_total_enabled")]
    public int BulletTotalEnabled { get; set; } = 1;

    /// <summary>
    /// Время агрегации пуль для общего подсчета (секунды)
    /// Диапазон: 0.05-5.0
    /// </summary>
    [JsonPropertyName("css_showdamage_bullet_aggregation_time")]
    public float BulletAggregationTime { get; set; } = 0.3f;

    /// <summary>
    /// Сообщение для общего урона от пуль
    /// {0} - общий урон, {1} - количество пораженных игроков
    /// </summary>
    [JsonPropertyName("css_showdamage_bullet_total_message")]
    public string BulletTotalMessage { get; set; } = "Общий урон: <font color='red'>{0} HP</font> (поражено: <font color='green'>{1} игроков</font>)";
}

[MinimumApiVersion(362)]
public class CS2_ShowDamage : BasePlugin, IPluginConfig<ShowDamageConfig>
{
    public override string ModuleName => "CS2 ShowDamage";
    public override string ModuleVersion => "3.0";
    public override string ModuleAuthor => "Fixed by le1t1337 + AI DeepSeek. Code logic by Ало, Ва-Вадик?, ABKAM, AbNeR CSS";

    private readonly Random _random = new();

    // Поля для HUD системы
    private readonly Dictionary<int, string> _playerMessages = new();
    private readonly Dictionary<int, Timer> _messageTimers = new();

    // Поля для суммарного урона от гранат и молотов (раздельно)
    private readonly Dictionary<int, GrenadeDamageInfo> _heGrenadeDamageInfo = new();
    private readonly Dictionary<int, GrenadeDamageInfo> _molotovDamageInfo = new();

    // Поля для суммарного урона от пуль
    private readonly Dictionary<int, BulletDamageAggregator> _bulletAggregators = new();

    // Для управления таймерами гранат
    private readonly Dictionary<int, Dictionary<string, Timer>> _timersByKey = new();

    // Для отслеживания времени первого попадания от молотова
    private readonly Dictionary<int, DateTime> _molotovFirstHitTime = new();

    // Для отслеживания выстрелов и пробитий
    private readonly Dictionary<int, ShotInfo> _shotInfo = new();

    // Для хранения последнего урона по каждой цели (атакующий -> цель -> суммарный урон)
    private readonly Dictionary<int, Dictionary<int, SingleTargetDamage>> _singleTargetDamage = new();

    public required ShowDamageConfig Config { get; set; }

    // Класс для хранения информации об уроне от гранат/молотов
    private class GrenadeDamageInfo
    {
        public int TotalDamage { get; set; }
        public HashSet<int> VictimSlots { get; set; } = new();
        public string WeaponType { get; set; } = string.Empty;
        public string LastWeaponName { get; set; } = string.Empty;
        public Timer? Timer { get; set; }
    }

    // Класс для агрегации урона от пуль
    private class BulletDamageAggregator
    {
        public int TotalDamage { get; set; }
        public HashSet<int> VictimSlots { get; set; } = new();
        public HashSet<int> KilledSlots { get; set; } = new();
        public Timer? Timer { get; set; }
        public string WeaponName { get; set; } = string.Empty;
        public DateTime LastDamageTime { get; set; } = DateTime.Now;
        public int CurrentTargetSlot { get; set; } = -1;
        public int CurrentTargetTotalDamage { get; set; }
        public bool IsPenetrationShot { get; set; } = false;
        public int ShotId { get; set; } = 0;
    }

    // Класс для отслеживания информации о выстреле
    private class ShotInfo
    {
        public DateTime FireTime { get; set; }
        public string WeaponName { get; set; } = string.Empty;
        public int HitCount { get; set; } = 0;
        public Timer? Timer { get; set; }
        public int ShotId { get; set; }
    }

    // Класс для хранения урона по одной цели
    private class SingleTargetDamage
    {
        public int TotalDamage { get; set; }
        public int LastHealth { get; set; }
        public bool IsHeadshot { get; set; }
        public Timer? Timer { get; set; }
        public DateTime LastDamageTime { get; set; }
        public bool IsDead { get; set; } = false;
    }

    public void OnConfigParsed(ShowDamageConfig config)
    {
        Config = config;

        // Валидация конфигурации
        Config.Enabled = Math.Clamp(Config.Enabled, 0, 1);
        Config.NotifyDuration = Math.Clamp(Config.NotifyDuration, 0.1f, 10.0f);
        Config.LogLevel = Math.Clamp(Config.LogLevel, 0, 5);
        Config.HudColorMode = Math.Clamp(Config.HudColorMode, 1, 1);
        Config.GrenadeTotalEnabled = Math.Clamp(Config.GrenadeTotalEnabled, 0, 1);
        Config.GrenadeTotalDuration = Math.Clamp(Config.GrenadeTotalDuration, 1.0f, 10.0f);
        Config.MolotovAggregationDuration = Math.Clamp(Config.MolotovAggregationDuration, 1.0f, 15.0f);
        Config.BulletTotalEnabled = Math.Clamp(Config.BulletTotalEnabled, 0, 1);
        Config.BulletAggregationTime = Math.Clamp(Config.BulletAggregationTime, 0.05f, 5.0f);

        Log(LogLevel.Information, $"Конфиг загружен: Включен={Config.Enabled}, Уровень логирования={Config.LogLevel}, Суммарный урон гранат={Config.GrenadeTotalEnabled}, Агрегация пуль={Config.BulletTotalEnabled}");
    }

    public override void Load(bool hotReload)
    {
        // Регистрируем команды
        AddCommand("css_showdamage_help", "Показать справку по плагину ShowDamage", OnHelpCommand);
        AddCommand("css_showdamage_settings", "Показать текущие настройки ShowDamage", OnSettingsCommand);
        AddCommand("css_showdamage_test", "Тестовая команда ShowDamage", OnTestCommand);
        AddCommand("css_showdamage_reload", "Перезагрузить конфигурацию ShowDamage", OnReloadCommand);
        AddCommand("css_showdamage_cleardamage", "Очистить суммарный урон от гранат, молотов и пуль", OnClearDamageCommand);
        AddCommand("css_showdamage_toggle", "Включить/выключить плагин ShowDamage (0/1)", OnToggleCommand);

        // Команды для изменения каждой переменной конфига
        AddCommand("css_showdamage_setenabled", "Установить включение плагина (0/1)", OnSetEnabledCommand);
        AddCommand("css_showdamage_setnotifyduration", "Установить длительность отображения (0.1-10.0)", OnSetNotifyDurationCommand);
        AddCommand("css_showdamage_setloglevel", "Установить уровень логирования (0-Trace,1-Debug,2-Info,3-Warning,4-Error,5-Critical)", OnSetLogLevelCommand);
        AddCommand("css_showdamage_sethudcolormode", "Установить режим цвета HUD (1)", OnSetHudColorModeCommand);
        AddCommand("css_showdamage_setgrenadetotalenabled", "Включить суммарный урон гранат (0/1)", OnSetGrenadeTotalEnabledCommand);
        AddCommand("css_showdamage_setgrenadetotalduration", "Установить длительность отображения суммарного урона гранат (1.0-10.0)", OnSetGrenadeTotalDurationCommand);
        AddCommand("css_showdamage_setmolotovaggregationduration", "Установить длительность агрегации молотова (1.0-15.0)", OnSetMolotovAggregationDurationCommand);
        AddCommand("css_showdamage_setgrenadetotalmessage", "Установить сообщение для суммарного урона от гранаты (строка)", OnSetGrenadeTotalMessageCommand);
        AddCommand("css_showdamage_setmolotovtotalmessage", "Установить сообщение для суммарного урона от молотова (строка)", OnSetMolotovTotalMessageCommand);
        AddCommand("css_showdamage_setbullettotalenabled", "Включить суммарный урон пуль (0/1)", OnSetBulletTotalEnabledCommand);
        AddCommand("css_showdamage_setbulletaggregationtime", "Установить время агрегации пуль (0.05-5.0)", OnSetBulletAggregationTimeCommand);
        AddCommand("css_showdamage_setbullettotalmessage", "Установить сообщение для суммарного урона от пуль (строка)", OnSetBulletTotalMessageCommand);

        // Регистрируем обработчик событий
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Post);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire, HookMode.Post);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Post);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Post);

        // Регистрируем тик для отображения HUD сообщений
        RegisterListener<Listeners.OnTick>(OnTick);

        if (hotReload)
        {
            Server.NextFrame(() => Log(LogLevel.Information, "Горячая перезагрузка выполнена"));
        }

        PrintConVarInfo();
    }

    private void PrintConVarInfo()
    {
        Log(LogLevel.Information, "===============================================");
        Log(LogLevel.Information, $"Плагин {ModuleName} версии {ModuleVersion} успешно загружен!");
        Log(LogLevel.Information, $"Автор: {ModuleAuthor}");
        Log(LogLevel.Information, "Текущие настройки:");
        Log(LogLevel.Information, $"  css_showdamage_enabled = {Config.Enabled} (0/1)");
        Log(LogLevel.Information, $"  css_showdamage_duration = {Config.NotifyDuration.ToString(CultureInfo.InvariantCulture)} сек.");
        Log(LogLevel.Information, $"  css_showdamage_log_level = {Config.LogLevel} (0-Trace,1-Debug,2-Info,3-Warning,4-Error,5-Critical)");
        Log(LogLevel.Information, $"  css_showdamage_hud_color_mode = {Config.HudColorMode}");
        Log(LogLevel.Information, $"  css_showdamage_grenade_total_enabled = {Config.GrenadeTotalEnabled} (0/1)");
        Log(LogLevel.Information, $"  css_showdamage_grenade_total_duration = {Config.GrenadeTotalDuration.ToString(CultureInfo.InvariantCulture)} сек.");
        Log(LogLevel.Information, $"  css_showdamage_molotov_aggregation_duration = {Config.MolotovAggregationDuration.ToString(CultureInfo.InvariantCulture)} сек.");
        Log(LogLevel.Information, $"  css_showdamage_grenade_total_message = {Config.GrenadeTotalMessage}");
        Log(LogLevel.Information, $"  css_showdamage_molotov_total_message = {Config.MolotovTotalMessage}");
        Log(LogLevel.Information, $"  css_showdamage_bullet_total_enabled = {Config.BulletTotalEnabled} (0/1)");
        Log(LogLevel.Information, $"  css_showdamage_bullet_aggregation_time = {Config.BulletAggregationTime.ToString(CultureInfo.InvariantCulture)} сек.");
        Log(LogLevel.Information, $"  css_showdamage_bullet_total_message = {Config.BulletTotalMessage}");
        Log(LogLevel.Information, "===============================================");
    }

    private void Log(LogLevel level, string message)
    {
        if ((int)level >= Config.LogLevel)
            Logger.Log(level, "[ShowDamage] {Message}", message);
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        try
        {
            if (Config.Enabled == 0)
                return HookResult.Continue;

            var victim = @event.Userid;
            var attacker = @event.Attacker;
            var weapon = @event.Weapon;

            if (victim == null || !victim.IsValid || attacker == null || !attacker.IsValid)
                return HookResult.Continue;

            if (attacker == victim || attacker.TeamNum == victim.TeamNum)
                return HookResult.Continue;

            var damage = @event.DmgHealth;
            var health = @event.Health;
            var isHeadshot = @event.Hitgroup == 1;
            var isKill = health <= 0;

            if (damage <= 0)
                return HookResult.Continue;

            Log(LogLevel.Debug, $"Урон: {attacker.PlayerName} -> {victim.PlayerName}: {damage} HP (оружие={weapon}, голова={isHeadshot}, убийство={isKill}, здоровье={health})");

            // Проверяем, является ли оружие HE-гранатой или молотовым
            if (Config.GrenadeTotalEnabled == 1 && IsDamageGrenade(weapon, out var grenadeType))
            {
                // Показываем мгновенный урон для гранат и молотов
                ShowSingleDamage(attacker, damage, health, isHeadshot, isKill);

                // Затем агрегируем для суммарного урона
                ProcessGrenadeDamage(attacker, victim, damage, weapon, grenadeType);
            }
            else if (Config.BulletTotalEnabled == 1)
            {
                // Обработка пуль
                ProcessBulletDamage(attacker, victim, damage, weapon, isHeadshot, isKill, health);
            }
            else
            {
                // Если агрегация отключена, показываем обычное сообщение
                ShowSingleDamage(attacker, damage, health, isHeadshot, isKill);
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в OnPlayerHurt: {ex.Message}");
        }

        return HookResult.Continue;
    }

    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        try
        {
            if (Config.Enabled == 0)
                return HookResult.Continue;

            var attacker = @event.Userid;
            if (attacker == null || !attacker.IsValid)
                return HookResult.Continue;

            int attackerSlot = attacker.Slot;
            string weapon = @event.Weapon;

            // Создаем новый выстрел
            int shotId = _random.Next(1, 1000000);
            var shotInfo = new ShotInfo
            {
                FireTime = DateTime.Now,
                WeaponName = weapon,
                HitCount = 0,
                ShotId = shotId
            };

            // Останавливаем старый таймер если есть
            if (_shotInfo.TryGetValue(attackerSlot, out var oldShotInfo) && oldShotInfo.Timer != null)
            {
                oldShotInfo.Timer.Kill();
            }

            // Создаем таймер для очистки информации о выстреле
            shotInfo.Timer = AddTimer(Config.BulletAggregationTime * 2, () =>
            {
                // При очистке выстрела показываем общий урон, если есть агрегатор и больше одной цели
                if (_bulletAggregators.TryGetValue(attackerSlot, out var aggregator) && aggregator.ShotId == shotId)
                {
                    // Показываем общий урон только если поражено больше 1 цели
                    if (aggregator.VictimSlots.Count > 1)
                    {
                        ShowTotalBulletDamage(attackerSlot);
                    }
                    else
                    {
                        _bulletAggregators.Remove(attackerSlot);
                    }
                }
                _shotInfo.Remove(attackerSlot);
            });

            _shotInfo[attackerSlot] = shotInfo;

            Log(LogLevel.Debug, $"Новый выстрел: {attacker.PlayerName} - {weapon} (ID: {shotId})");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в OnWeaponFire: {ex.Message}");
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        try
        {
            if (Config.Enabled == 0)
                return HookResult.Continue;

            var victim = @event.Userid;
            var attacker = @event.Attacker;

            if (victim == null || !victim.IsValid || attacker == null || !attacker.IsValid)
                return HookResult.Continue;

            if (attacker == victim || attacker.TeamNum == victim.TeamNum)
                return HookResult.Continue;

            var weapon = @event.Weapon;
            var isHeadshot = @event.Headshot;

            Log(LogLevel.Debug, $"Смерть: {attacker.PlayerName} -> {victim.PlayerName} (оружие={weapon}, голова={isHeadshot})");

            // Проверяем, есть ли информация об уроне от этого выстрела
            int attackerSlot = attacker.Slot;
            int victimSlot = victim.Slot;

            // Проверяем, есть ли запись об уроне по этой цели
            if (_singleTargetDamage.TryGetValue(attackerSlot, out var attackerDict) &&
                attackerDict.TryGetValue(victimSlot, out var targetDamage))
            {
                // Если есть информация об уроне, показываем ее с меткой УБИТ
                ShowKillMessage(attacker, victimSlot, targetDamage.TotalDamage, targetDamage.IsHeadshot || isHeadshot);

                // Помечаем цель как убитую
                targetDamage.IsDead = true;

                // Останавливаем таймер очистки если есть
                if (targetDamage.Timer != null)
                {
                    targetDamage.Timer.Kill();
                    targetDamage.Timer = null;
                }

                // Не удаляем сразу, даем показать сообщение
                AddTimer(Config.NotifyDuration + 0.1f, () =>
                {
                    if (_singleTargetDamage.TryGetValue(attackerSlot, out var dict) && dict.ContainsKey(victimSlot))
                    {
                        dict.Remove(victimSlot);
                        if (dict.Count == 0)
                        {
                            _singleTargetDamage.Remove(attackerSlot);
                        }
                    }
                });
            }
            else
            {
                // Если нет информации об уроне (убийство с одного выстрела), создаем временную запись об убийстве
                Log(LogLevel.Debug, $"Убийство с одного выстрела: {attacker.PlayerName} -> {victim.PlayerName}");

                // Предполагаем, что это был хедшот для сильного оружия
                int damageToShow = 100;
                string weaponLower = weapon.ToLower();
                if (weaponLower.Contains("awp") || weaponLower.Contains("ssg08") || weaponLower.Contains("scout"))
                {
                    damageToShow = isHeadshot ? 150 : 100;
                }
                else if (weaponLower.Contains("ak47") || weaponLower.Contains("m4a1"))
                {
                    damageToShow = isHeadshot ? 140 : 35;
                }
                else if (weaponLower.Contains("deagle"))
                {
                    damageToShow = isHeadshot ? 140 : 60;
                }

                // Показываем сообщение об убийстве
                ShowKillMessage(attacker, victimSlot, damageToShow, isHeadshot);

                // Создаем запись об уроне для возможных последующих событий
                if (!_singleTargetDamage.ContainsKey(attackerSlot))
                {
                    _singleTargetDamage[attackerSlot] = new Dictionary<int, SingleTargetDamage>();
                }

                var newTargetDamage = new SingleTargetDamage
                {
                    TotalDamage = damageToShow,
                    LastHealth = 0,
                    IsHeadshot = isHeadshot,
                    IsDead = true,
                    LastDamageTime = DateTime.Now
                };

                _singleTargetDamage[attackerSlot][victimSlot] = newTargetDamage;

                // Удаляем через время
                AddTimer(Config.NotifyDuration + 0.1f, () =>
                {
                    if (_singleTargetDamage.TryGetValue(attackerSlot, out var dict) && dict.ContainsKey(victimSlot))
                    {
                        dict.Remove(victimSlot);
                        if (dict.Count == 0)
                        {
                            _singleTargetDamage.Remove(attackerSlot);
                        }
                    }
                });
            }

            // Также очищаем информацию при смерти игрока
            CleanupPlayerData(victimSlot);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в OnPlayerDeath: {ex.Message}");
        }

        return HookResult.Continue;
    }

    private void ShowKillMessage(CCSPlayerController attacker, int victimSlot, int damage, bool isHeadshot)
    {
        try
        {
            int attackerSlot = attacker.Slot;
            string dmgColor = GetDamageColor(damage, isHeadshot, true);

            string message = $"-<font color='{dmgColor}'>{damage}</font> <font color='white'>HP</font> [УБИТ]";

            _playerMessages[attackerSlot] = message;

            if (_messageTimers.TryGetValue(attackerSlot, out var oldTimer))
            {
                oldTimer.Kill();
                _messageTimers.Remove(attackerSlot);
            }

            var newTimer = AddTimer(Config.NotifyDuration, () =>
            {
                if (_playerMessages.TryGetValue(attackerSlot, out var currentMessage) && currentMessage == message)
                {
                    _playerMessages.Remove(attackerSlot);
                }
                _messageTimers.Remove(attackerSlot);
            });

            _messageTimers[attackerSlot] = newTimer;

            Log(LogLevel.Debug, $"Показано сообщение об убийстве: {attacker.PlayerName} - {damage} HP [УБИТ]");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в ShowKillMessage: {ex.Message}");
        }
    }

    private bool IsDamageGrenade(string weaponName, out string grenadeType)
    {
        grenadeType = string.Empty;

        if (string.IsNullOrEmpty(weaponName))
            return false;

        string lowerWeapon = weaponName.ToLower();

        if (lowerWeapon.Contains("hegrenade"))
        {
            grenadeType = "hegrenade";
            return true;
        }
        else if (lowerWeapon.Contains("molotov") || lowerWeapon.Contains("incgrenade") || lowerWeapon.Contains("inferno"))
        {
            grenadeType = "molotov";
            return true;
        }

        return false;
    }

    private void ProcessGrenadeDamage(CCSPlayerController attacker, CCSPlayerController victim, int damage, string weaponName, string grenadeType)
    {
        try
        {
            int attackerSlot = attacker.Slot;
            int victimSlot = victim.Slot;

            Dictionary<int, GrenadeDamageInfo> damageDict = grenadeType == "hegrenade"
                ? _heGrenadeDamageInfo
                : _molotovDamageInfo;

            if (!damageDict.ContainsKey(attackerSlot))
            {
                damageDict[attackerSlot] = new GrenadeDamageInfo
                {
                    WeaponType = grenadeType,
                    LastWeaponName = weaponName
                };

                if (grenadeType == "molotov")
                {
                    _molotovFirstHitTime[attackerSlot] = DateTime.Now;

                    var timerKey = $"molotov_{attackerSlot}";
                    var newTimer = AddTimer(Config.MolotovAggregationDuration, () =>
                    {
                        ShowTotalGrenadeDamage(attackerSlot, "molotov");
                        RemoveTimerKey(attackerSlot, timerKey);
                        _molotovFirstHitTime.Remove(attackerSlot);
                    });

                    SetTimerKey(attackerSlot, timerKey, newTimer);
                    Log(LogLevel.Debug, $"Запущен таймер молотова для {attacker.PlayerName} на {Config.MolotovAggregationDuration} секунд");
                }
                else
                {
                    var timerKey = $"hegrenade_{attackerSlot}";
                    var newTimer = AddTimer(Config.GrenadeTotalDuration, () =>
                    {
                        ShowTotalGrenadeDamage(attackerSlot, "hegrenade");
                        RemoveTimerKey(attackerSlot, timerKey);
                    });

                    SetTimerKey(attackerSlot, timerKey, newTimer);
                }
            }

            var grenadeInfo = damageDict[attackerSlot];
            grenadeInfo.TotalDamage += damage;
            grenadeInfo.VictimSlots.Add(victimSlot);
            grenadeInfo.LastWeaponName = weaponName;

            if (grenadeType == "hegrenade")
            {
                var timerKey = $"hegrenade_{attackerSlot}";
                var oldTimer = FindTimerByKey(attackerSlot, timerKey);
                if (oldTimer != null)
                {
                    oldTimer.Kill();

                    var newTimer = AddTimer(Config.GrenadeTotalDuration, () =>
                    {
                        ShowTotalGrenadeDamage(attackerSlot, "hegrenade");
                        RemoveTimerKey(attackerSlot, timerKey);
                    });

                    SetTimerKey(attackerSlot, timerKey, newTimer);
                }
            }

            string weaponNameRU = grenadeType == "hegrenade" ? "граната" : "молотовый";
            Log(LogLevel.Debug, $"{weaponNameRU}: {attacker.PlayerName} -> {victim.PlayerName}: {damage} HP (общий={grenadeInfo.TotalDamage}, игроков={grenadeInfo.VictimSlots.Count})");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в ProcessGrenadeDamage: {ex.Message}");
        }
    }

    private void ProcessBulletDamage(CCSPlayerController attacker, CCSPlayerController victim, int damage, string weaponName, bool isHeadshot, bool isKill, int health)
    {
        try
        {
            int attackerSlot = attacker.Slot;
            int victimSlot = victim.Slot;

            // Проверяем, есть ли информация о текущем выстреле
            bool isPenetrationShot = false;
            int shotId = 0;

            if (_shotInfo.TryGetValue(attackerSlot, out var shot))
            {
                shotId = shot.ShotId;
                shot.HitCount++;

                // Если это второй и последующий хит от одного выстрела - это пробитие
                if (shot.HitCount > 1)
                {
                    isPenetrationShot = true;
                    Log(LogLevel.Debug, $"Обнаружено пробитие: {attacker.PlayerName} - выстрел #{shot.ShotId}, хит #{shot.HitCount}");
                }
            }
            else
            {
                // Если нет информации о выстреле, создаем ее (на случай, если событие WeaponFire не сработало)
                shotId = _random.Next(1, 1000000);
                shot = new ShotInfo
                {
                    FireTime = DateTime.Now,
                    WeaponName = weaponName,
                    HitCount = 1,
                    ShotId = shotId
                };
                _shotInfo[attackerSlot] = shot;
            }

            // Получаем или создаем агрегатор для текущего выстрела
            if (!_bulletAggregators.TryGetValue(attackerSlot, out var aggregator) || aggregator.ShotId != shotId)
            {
                // Если агрегатор существует, но для другого выстрела, показываем его результаты
                if (aggregator != null && aggregator.ShotId != shotId)
                {
                    // Показываем общий урон только если поражено больше 1 цели
                    if (aggregator.VictimSlots.Count > 1)
                    {
                        ShowTotalBulletDamage(attackerSlot);
                    }
                    else
                    {
                        _bulletAggregators.Remove(attackerSlot);
                    }
                    aggregator = null;
                }

                if (aggregator == null)
                {
                    // Создаем новый агрегатор для текущего выстрела
                    aggregator = new BulletDamageAggregator
                    {
                        WeaponName = weaponName,
                        CurrentTargetSlot = victimSlot,
                        CurrentTargetTotalDamage = 0,
                        IsPenetrationShot = isPenetrationShot,
                        ShotId = shotId
                    };
                    _bulletAggregators[attackerSlot] = aggregator;
                }
            }

            // Проверяем, та же ли это цель
            if (aggregator.CurrentTargetSlot != victimSlot)
            {
                // Новая цель
                aggregator.CurrentTargetSlot = victimSlot;
                aggregator.CurrentTargetTotalDamage = 0;
            }

            // Увеличиваем суммарный урон по текущей цели
            aggregator.CurrentTargetTotalDamage += damage;

            // Обновляем общую статистику
            aggregator.TotalDamage += damage;
            aggregator.VictimSlots.Add(victimSlot);
            aggregator.LastDamageTime = DateTime.Now;

            // Добавляем в убитые, если это убийство
            if (isKill)
            {
                aggregator.KilledSlots.Add(victimSlot);
            }

            // Обновляем флаг пробития если нужно
            if (isPenetrationShot)
            {
                aggregator.IsPenetrationShot = true;
            }

            // Останавливаем старый таймер если есть
            if (aggregator.Timer != null)
            {
                aggregator.Timer.Kill();
                aggregator.Timer = null;
            }

            // Управляем уроном по одной цели (суммируем урон)
            UpdateSingleTargetDamage(attackerSlot, victimSlot, damage, health, isHeadshot, isKill);

            // Показываем НАКОПЛЕННЫЙ урон по текущей цели
            ShowSingleTargetDamage(attacker, victimSlot, isKill);

            // Создаем новый таймер для отображения общего урона (только если есть пробитие или несколько целей)
            aggregator.Timer = AddTimer(Config.BulletAggregationTime, () =>
            {
                // Показываем общий урон только если поражено больше 1 цели
                if (aggregator.VictimSlots.Count > 1)
                {
                    ShowTotalBulletDamage(attackerSlot);
                }
                else
                {
                    // Если поражена только одна цель, просто очищаем
                    _bulletAggregators.Remove(attackerSlot);
                }
            });

            Log(LogLevel.Debug, $"Пуля: {attacker.PlayerName} -> {victim.PlayerName}: {damage} HP (убийство={isKill}, пробитие={aggregator.IsPenetrationShot}, выстрел={shotId}, суммарный по цели={aggregator.CurrentTargetTotalDamage}, общий={aggregator.TotalDamage}, целей={aggregator.VictimSlots.Count}, убито={aggregator.KilledSlots.Count})");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в ProcessBulletDamage: {ex.Message}");
        }
    }

    private void UpdateSingleTargetDamage(int attackerSlot, int victimSlot, int damage, int health, bool isHeadshot, bool isKill)
    {
        try
        {
            // Инициализируем словарь для атакующего, если его нет
            if (!_singleTargetDamage.ContainsKey(attackerSlot))
            {
                _singleTargetDamage[attackerSlot] = new Dictionary<int, SingleTargetDamage>();
            }

            var attackerDict = _singleTargetDamage[attackerSlot];

            // Получаем или создаем информацию об уроне по цели
            if (!attackerDict.TryGetValue(victimSlot, out var targetDamage))
            {
                targetDamage = new SingleTargetDamage
                {
                    TotalDamage = 0,
                    LastHealth = 100, // Предполагаем начальное здоровье
                    IsHeadshot = false,
                    LastDamageTime = DateTime.Now,
                    IsDead = false
                };
                attackerDict[victimSlot] = targetDamage;
            }

            // Обновляем информацию
            targetDamage.TotalDamage += damage;
            targetDamage.LastHealth = health;
            targetDamage.IsHeadshot = targetDamage.IsHeadshot || isHeadshot;
            targetDamage.LastDamageTime = DateTime.Now;

            // Если это убийство, помечаем цель как убитую
            if (isKill)
            {
                targetDamage.IsDead = true;
            }

            // Останавливаем старый таймер если есть
            if (targetDamage.Timer != null)
            {
                targetDamage.Timer.Kill();
                targetDamage.Timer = null;
            }

            // Если это не убийство, создаем таймер для очистки
            if (!isKill)
            {
                targetDamage.Timer = AddTimer(Config.NotifyDuration * 2, () =>
                {
                    if (_singleTargetDamage.TryGetValue(attackerSlot, out var dict) && dict.ContainsKey(victimSlot))
                    {
                        dict.Remove(victimSlot);
                        if (dict.Count == 0)
                        {
                            _singleTargetDamage.Remove(attackerSlot);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в UpdateSingleTargetDamage: {ex.Message}");
        }
    }

    private void ShowSingleTargetDamage(CCSPlayerController attacker, int victimSlot, bool isKill)
    {
        try
        {
            int attackerSlot = attacker.Slot;

            if (!_singleTargetDamage.TryGetValue(attackerSlot, out var attackerDict) ||
                !attackerDict.TryGetValue(victimSlot, out var targetDamage))
            {
                return;
            }

            string dmgColor = GetDamageColor(targetDamage.TotalDamage, targetDamage.IsHeadshot, isKill);

            string message = $"-<font color='{dmgColor}'>{targetDamage.TotalDamage}</font> <font color='white'>HP</font>";

            if (!isKill)
            {
                message += $" [{targetDamage.LastHealth} HP]";
            }
            else
            {
                message += " [УБИТ]";
            }

            _playerMessages[attackerSlot] = message;

            if (_messageTimers.TryGetValue(attackerSlot, out var oldTimer))
            {
                oldTimer.Kill();
                _messageTimers.Remove(attackerSlot);
            }

            var newTimer = AddTimer(Config.NotifyDuration, () =>
            {
                if (_playerMessages.TryGetValue(attackerSlot, out var currentMessage) && currentMessage == message)
                {
                    _playerMessages.Remove(attackerSlot);
                }
                _messageTimers.Remove(attackerSlot);
            });

            _messageTimers[attackerSlot] = newTimer;

            // Если это убийство, очищаем информацию об уроне по этой цели после отображения
            if (isKill)
            {
                AddTimer(Config.NotifyDuration + 0.1f, () =>
                {
                    if (_singleTargetDamage.TryGetValue(attackerSlot, out var dict) && dict.ContainsKey(victimSlot))
                    {
                        dict.Remove(victimSlot);
                        if (dict.Count == 0)
                        {
                            _singleTargetDamage.Remove(attackerSlot);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в ShowSingleTargetDamage: {ex.Message}");
        }
    }

    private void ShowSingleDamage(CCSPlayerController attacker, int damage, int health, bool isHeadshot, bool isKill)
    {
        // Этот метод теперь используется только для гранат
        try
        {
            int slot = attacker.Slot;
            string dmgColor = GetDamageColor(damage, isHeadshot, isKill);

            string message = $"-<font color='{dmgColor}'>{damage}</font> <font color='white'>HP</font>";

            if (!isKill)
            {
                message += $" [{health} HP]";
            }
            else
            {
                message += " [УБИТ]";
            }

            _playerMessages[slot] = message;

            if (_messageTimers.TryGetValue(slot, out var oldTimer))
            {
                oldTimer.Kill();
                _messageTimers.Remove(slot);
            }

            var newTimer = AddTimer(Config.NotifyDuration, () =>
            {
                if (_playerMessages.TryGetValue(slot, out var currentMessage) && currentMessage == message)
                {
                    _playerMessages.Remove(slot);
                }
                _messageTimers.Remove(slot);
            });

            _messageTimers[slot] = newTimer;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в ShowSingleDamage: {ex.Message}");
        }
    }

    private void ShowTotalGrenadeDamage(int attackerSlot, string grenadeType)
    {
        try
        {
            Dictionary<int, GrenadeDamageInfo> damageDict = grenadeType == "hegrenade"
                ? _heGrenadeDamageInfo
                : _molotovDamageInfo;

            if (!damageDict.TryGetValue(attackerSlot, out var grenadeInfo) || grenadeInfo.TotalDamage <= 0)
                return;

            var attacker = Utilities.GetPlayerFromSlot(attackerSlot);
            if (attacker == null || !attacker.IsValid)
            {
                Log(LogLevel.Debug, $"Игрок со слотом {attackerSlot} не найден при попытке показать общий урон от {grenadeType}");
                return;
            }

            string messageTemplate = grenadeType == "hegrenade"
                ? Config.GrenadeTotalMessage
                : Config.MolotovTotalMessage;

            string message = string.Format(messageTemplate, grenadeInfo.TotalDamage, grenadeInfo.VictimSlots.Count);

            _playerMessages[attackerSlot] = message;

            AddTimer(Config.GrenadeTotalDuration, () =>
            {
                if (_playerMessages.TryGetValue(attackerSlot, out var currentMessage) && currentMessage == message)
                {
                    _playerMessages.Remove(attackerSlot);
                }
            });

            if (grenadeInfo.Timer != null)
            {
                grenadeInfo.Timer.Kill();
            }

            damageDict.Remove(attackerSlot);

            string weaponNameRU = grenadeType == "hegrenade" ? "гранаты" : "молотового";
            Log(LogLevel.Information, $"Суммарный урон {weaponNameRU}: {attacker.PlayerName} - {grenadeInfo.TotalDamage} HP (поражено {grenadeInfo.VictimSlots.Count} игроков)");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в ShowTotalGrenadeDamage: {ex.Message}");
        }
    }

    private void ShowTotalBulletDamage(int attackerSlot)
    {
        try
        {
            if (!_bulletAggregators.TryGetValue(attackerSlot, out var aggregator) || aggregator.TotalDamage <= 0)
                return;

            var attacker = Utilities.GetPlayerFromSlot(attackerSlot);
            if (attacker == null || !attacker.IsValid)
                return;

            // Показываем общий урон только если поражено больше 1 цели
            if (aggregator.VictimSlots.Count > 1)
            {
                string penetrationNote = aggregator.IsPenetrationShot && aggregator.VictimSlots.Count > 1 ? " (пробитие)" : "";

                string message = string.Format(Config.BulletTotalMessage, aggregator.TotalDamage, aggregator.VictimSlots.Count) + penetrationNote;

                _playerMessages[attackerSlot] = message;

                AddTimer(Config.NotifyDuration * 1.5f, () =>
                {
                    _playerMessages.Remove(attackerSlot);
                });

                Log(LogLevel.Information, $"Суммарный урон пуль{penetrationNote}: {attacker.PlayerName} - {aggregator.TotalDamage} HP (поражено {aggregator.VictimSlots.Count} игроков, убито {aggregator.KilledSlots.Count})");
            }

            _bulletAggregators.Remove(attackerSlot);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в ShowTotalBulletDamage: {ex.Message}");
        }
    }

    private string GetDamageColor(int damage, bool isHeadshot, bool isKill)
    {
        return damage switch
        {
            <= 25 => "green",
            <= 50 => "yellow",
            <= 75 => "orange",
            _ => "red"
        };
    }

    private void OnTick()
    {
        try
        {
            if (Config.Enabled == 0)
                return;

            foreach (var kvp in _playerMessages.ToList())
            {
                int slot = kvp.Key;
                string message = kvp.Value;

                var player = Utilities.GetPlayerFromSlot(slot);
                if (player != null && player.IsValid)
                {
                    player.PrintToCenterHtml(message);
                }
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в OnTick: {ex.Message}");
        }
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        try
        {
            if (Config.Enabled == 0)
                return HookResult.Continue;

            ClearHudMessages();
            ClearAllDamage();
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в OnRoundEnd: {ex.Message}");
        }
        return HookResult.Continue;
    }

    private void CleanupPlayerData(int slot)
    {
        try
        {
            // Очищаем только сообщения об уроне для убитого игрока, но не общие сообщения об уроне
            if (_playerMessages.TryGetValue(slot, out var message) &&
                !message.Contains("Общий урон от") && !message.Contains("Общий урон:"))
            {
                _playerMessages.Remove(slot);
            }

            if (_messageTimers.TryGetValue(slot, out var timer))
            {
                timer.Kill();
                _messageTimers.Remove(slot);
            }

            // Очищаем информацию о гранатах и пулях для убитого игрока (как атакующего)
            _heGrenadeDamageInfo.Remove(slot);
            _molotovDamageInfo.Remove(slot);
            _molotovFirstHitTime.Remove(slot);
            _bulletAggregators.Remove(slot);
            _shotInfo.Remove(slot);

            // Очищаем информацию об уроне по целям для убитого игрока
            _singleTargetDamage.Remove(slot);

            if (_timersByKey.ContainsKey(slot))
            {
                foreach (var t in _timersByKey[slot].Values)
                {
                    t.Kill();
                }
                _timersByKey.Remove(slot);
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в CleanupPlayerData: {ex.Message}");
        }
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        try
        {
            var player = @event.Userid;
            if (player != null && player.IsValid)
            {
                int slot = player.Slot;
                CleanupPlayerData(slot);
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка в OnPlayerDisconnect: {ex.Message}");
        }
        return HookResult.Continue;
    }

    private Timer? FindTimerByKey(int slot, string key)
    {
        if (_timersByKey.TryGetValue(slot, out var slotTimers) && slotTimers.TryGetValue(key, out var timer))
        {
            return timer;
        }
        return null;
    }

    private void SetTimerKey(int slot, string key, Timer timer)
    {
        if (!_timersByKey.ContainsKey(slot))
        {
            _timersByKey[slot] = new Dictionary<string, Timer>();
        }
        _timersByKey[slot][key] = timer;
    }

    private void RemoveTimerKey(int slot, string key)
    {
        if (_timersByKey.TryGetValue(slot, out var slotTimers))
        {
            slotTimers.Remove(key);
        }
    }

    // ---------- Команды ----------

    private void OnHelpCommand(CCSPlayerController? player, CommandInfo command)
    {
        string helpMessage = $"""
            ================================================
            СПРАВКА ПО ПЛАГИНУ {ModuleName} v{ModuleVersion}
            ================================================
            ОПИСАНИЕ:
              Показывает нанесённый урон в HUD игроку. Поддерживает суммирование
              урона от гранат, молотовых и пуль (включая пробития).

            КОНФИГУРАЦИОННЫЙ ФАЙЛ:
              addons/counterstrikesharp/configs/plugins/CS2_ShowDamage/ShowDamageConfig.json

            КОНСОЛЬНЫЕ КОМАНДЫ:
              css_showdamage_help                - показать эту справку
              css_showdamage_settings             - показать текущие настройки
              css_showdamage_test                  - проверить работоспособность
              css_showdamage_reload                - перезагрузить конфигурацию
              css_showdamage_cleardamage           - очистить все накопленные данные об уроне
              css_showdamage_toggle <0/1>          - включить/выключить плагин
              css_showdamage_setenabled <0/1>      - установить состояние плагина
              css_showdamage_setnotifyduration <0.1-10.0> - длительность отображения HUD
              css_showdamage_setloglevel <0-5>     - уровень логирования
              css_showdamage_sethudcolormode <1>   - режим цвета HUD (только 1)
              css_showdamage_setgrenadetotalenabled <0/1> - суммарный урон гранат
              css_showdamage_setgrenadetotalduration <1.0-10.0> - длительность показа суммы гранат
              css_showdamage_setmolotovaggregationduration <1.0-15.0> - время агрегации молотова
              css_showdamage_setgrenadetotalmessage <строка> - сообщение для HE гранаты
              css_showdamage_setmolotovtotalmessage <строка> - сообщение для молотова
              css_showdamage_setbullettotalenabled <0/1> - суммарный урон пуль
              css_showdamage_setbulletaggregationtime <0.05-5.0> - время агрегации пуль
              css_showdamage_setbullettotalmessage <строка> - сообщение для пуль

            ПРИМЕРЫ:
              css_showdamage_setenabled 1
              css_showdamage_setnotifyduration 2.5
              css_showdamage_setloglevel 2
            ================================================
            """;
        command.ReplyToCommand(helpMessage);
        if (player != null)
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}Справка отправлена в консоль.");
    }

    private void OnSettingsCommand(CCSPlayerController? player, CommandInfo command)
    {
        int activeHEGrenades = _heGrenadeDamageInfo.Count;
        int activeMolotovs = _molotovDamageInfo.Count;
        int activeBullets = _bulletAggregators.Count;
        int totalGrenadePlayers = _heGrenadeDamageInfo.Sum(x => x.Value.VictimSlots.Count) + _molotovDamageInfo.Sum(x => x.Value.VictimSlots.Count);
        int totalGrenadeDamage = _heGrenadeDamageInfo.Sum(x => x.Value.TotalDamage) + _molotovDamageInfo.Sum(x => x.Value.TotalDamage);
        int totalBulletPlayers = _bulletAggregators.Sum(x => x.Value.VictimSlots.Count);
        int totalBulletDamage = _bulletAggregators.Sum(x => x.Value.TotalDamage);
        int totalKilled = _bulletAggregators.Sum(x => x.Value.KilledSlots.Count);
        int singleTargetEntries = _singleTargetDamage.Sum(x => x.Value.Count);

        string settingsMessage = $"""
            ================================================
            ТЕКУЩИЕ НАСТРОЙКИ {ModuleName} v{ModuleVersion}
            ================================================
            Плагин включен: {Config.Enabled} (0/1)
            Длительность HUD: {Config.NotifyDuration.ToString(CultureInfo.InvariantCulture)} сек.
            Уровень логирования: {Config.LogLevel} (0-Trace,1-Debug,2-Info,3-Warning,4-Error,5-Critical)
            Режим цвета HUD: {Config.HudColorMode}
            Суммарный урон гранат: {Config.GrenadeTotalEnabled} (0/1)
            Длительность показа суммы гранат: {Config.GrenadeTotalDuration.ToString(CultureInfo.InvariantCulture)} сек.
            Время агрегации молотова: {Config.MolotovAggregationDuration.ToString(CultureInfo.InvariantCulture)} сек.
            Сообщение HE гранаты: {Config.GrenadeTotalMessage}
            Сообщение молотова: {Config.MolotovTotalMessage}
            Суммарный урон пуль: {Config.BulletTotalEnabled} (0/1)
            Время агрегации пуль: {Config.BulletAggregationTime.ToString(CultureInfo.InvariantCulture)} сек.
            Сообщение пуль: {Config.BulletTotalMessage}

            АКТИВНЫЕ ДАННЫЕ:
            Активных HUD сообщений: {_playerMessages.Count}
            Активных HE гранат: {activeHEGrenades}
            Активных молотовых: {activeMolotovs}
            Активных агрегаторов пуль: {activeBullets}
            Записей урона по одной цели: {singleTargetEntries}
            Суммарный урон от гранат: {totalGrenadeDamage} HP ({totalGrenadePlayers} игроков)
            Суммарный урон от пуль: {totalBulletDamage} HP ({totalBulletPlayers} игроков, убито: {totalKilled})
            ================================================
            """;
        command.ReplyToCommand(settingsMessage);
        if (player != null)
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}Настройки отправлены в консоль.");
    }

    private void OnTestCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null)
        {
            command.ReplyToCommand("[ShowDamage] Эта команда доступна только игрокам.");
            return;
        }

        player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}=== ТЕСТ ПЛАГИНА ===");
        player.PrintToChat($" Плагин работает, текущие настройки:");
        player.PrintToChat($"   Включен: {Config.Enabled}");
        player.PrintToChat($"   Длительность HUD: {Config.NotifyDuration.ToString(CultureInfo.InvariantCulture)} сек.");
        player.PrintToChat($"   Уровень логов: {Config.LogLevel}");
        player.PrintToChat($"   Суммарный урон гранат: {Config.GrenadeTotalEnabled}");
        player.PrintToChat($"   Суммарный урон пуль: {Config.BulletTotalEnabled}");
        player.PrintToChat($" Отправляю тестовое сообщение в HUD...");

        // Отправляем тестовое сообщение
        _playerMessages[player.Slot] = "<font color='green'>Тест ShowDamage</font>";
        AddTimer(2.0f, () =>
        {
            _playerMessages.Remove(player.Slot);
        });

        command.ReplyToCommand("[ShowDamage] Тестовая информация выведена в чат и HUD.");
    }

    private void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        try
        {
            string configPath = Path.Combine(Server.GameDirectory, "counterstrikesharp", "configs", "plugins", "CS2_ShowDamage", "ShowDamageConfig.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var newConfig = System.Text.Json.JsonSerializer.Deserialize<ShowDamageConfig>(json);
                if (newConfig != null)
                {
                    OnConfigParsed(newConfig);
                    SaveConfig(); // сохраняем обратно, чтобы применить валидацию
                }
            }
            else
            {
                SaveConfig(); // создаст с настройками по умолчанию
            }

            ClearHudMessages();
            ClearAllDamage();

            command.ReplyToCommand("[ShowDamage] Конфигурация перезагружена.");
            Log(LogLevel.Information, "Конфигурация перезагружена по команде.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Ошибка при перезагрузке конфига: {ex.Message}");
            command.ReplyToCommand("[ShowDamage] Ошибка при перезагрузке конфига.");
        }
    }

    private void OnClearDamageCommand(CCSPlayerController? player, CommandInfo command)
    {
        int clearedCount = _heGrenadeDamageInfo.Count + _molotovDamageInfo.Count + _bulletAggregators.Count;
        ClearAllDamage();

        string message = $"Очищено {clearedCount} записей суммарного урона.";
        command.ReplyToCommand($"[ShowDamage] {message}");
        if (player != null)
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}{message}");
    }

    private void OnToggleCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее состояние: {Config.Enabled}. Использование: css_showdamage_toggle <0/1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && (value == 0 || value == 1))
        {
            int old = Config.Enabled;
            Config.Enabled = value;
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] Плагин изменён с {old} на {value}.");

            if (Config.Enabled == 0)
            {
                ClearHudMessages();
                ClearAllDamage();
            }
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Используйте 0 или 1.");
        }
    }

    private void OnSetEnabledCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение enabled: {Config.Enabled}. Использование: css_showdamage_setenabled <0/1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && (value == 0 || value == 1))
        {
            int old = Config.Enabled;
            Config.Enabled = value;
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] enabled изменён с {old} на {value}.");

            if (Config.Enabled == 0)
            {
                ClearHudMessages();
                ClearAllDamage();
            }
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Используйте 0 или 1.");
        }
    }

    private void OnSetNotifyDurationCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение duration: {Config.NotifyDuration.ToString(CultureInfo.InvariantCulture)}. Использование: css_showdamage_setnotifyduration <0.1-10.0>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.NotifyDuration;
            Config.NotifyDuration = Math.Clamp(value, 0.1f, 10.0f);
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] duration изменён с {old.ToString(CultureInfo.InvariantCulture)} на {Config.NotifyDuration.ToString(CultureInfo.InvariantCulture)}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Введите число с точкой (например 2.5).");
        }
    }

    private void OnSetLogLevelCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущий уровень логов: {Config.LogLevel} (0-Trace,1-Debug,2-Info,3-Warning,4-Error,5-Critical). Использование: css_showdamage_setloglevel <0-5>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && value >= 0 && value <= 5)
        {
            int old = Config.LogLevel;
            Config.LogLevel = value;
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] Уровень логов изменён с {old} на {value}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Используйте число от 0 до 5.");
        }
    }

    private void OnSetHudColorModeCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение hud_color_mode: {Config.HudColorMode}. Использование: css_showdamage_sethudcolormode <1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && value == 1)
        {
            int old = Config.HudColorMode;
            Config.HudColorMode = value;
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] hud_color_mode изменён с {old} на {value}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Допустимо только 1.");
        }
    }

    private void OnSetGrenadeTotalEnabledCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение grenade_total_enabled: {Config.GrenadeTotalEnabled}. Использование: css_showdamage_setgrenadetotalenabled <0/1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && (value == 0 || value == 1))
        {
            int old = Config.GrenadeTotalEnabled;
            Config.GrenadeTotalEnabled = value;
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] grenade_total_enabled изменён с {old} на {value}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Используйте 0 или 1.");
        }
    }

    private void OnSetGrenadeTotalDurationCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение grenade_total_duration: {Config.GrenadeTotalDuration.ToString(CultureInfo.InvariantCulture)}. Использование: css_showdamage_setgrenadetotalduration <1.0-10.0>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.GrenadeTotalDuration;
            Config.GrenadeTotalDuration = Math.Clamp(value, 1.0f, 10.0f);
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] grenade_total_duration изменён с {old.ToString(CultureInfo.InvariantCulture)} на {Config.GrenadeTotalDuration.ToString(CultureInfo.InvariantCulture)}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Введите число с точкой (например 4.5).");
        }
    }

    private void OnSetMolotovAggregationDurationCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение molotov_aggregation_duration: {Config.MolotovAggregationDuration.ToString(CultureInfo.InvariantCulture)}. Использование: css_showdamage_setmolotovaggregationduration <1.0-15.0>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.MolotovAggregationDuration;
            Config.MolotovAggregationDuration = Math.Clamp(value, 1.0f, 15.0f);
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] molotov_aggregation_duration изменён с {old.ToString(CultureInfo.InvariantCulture)} на {Config.MolotovAggregationDuration.ToString(CultureInfo.InvariantCulture)}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Введите число с точкой (например 7.0).");
        }
    }

    private void OnSetGrenadeTotalMessageCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее сообщение: {Config.GrenadeTotalMessage}. Использование: css_showdamage_setgrenadetotalmessage <текст>");
            return;
        }

        string old = Config.GrenadeTotalMessage;
        string value = command.GetArg(1);
        // Можно объединить несколько аргументов, если сообщение содержит пробелы
        for (int i = 2; i < command.ArgCount; i++)
            value += " " + command.GetArg(i);

        Config.GrenadeTotalMessage = value;
        SaveConfig();
        command.ReplyToCommand($"[ShowDamage] grenade_total_message изменён.");
    }

    private void OnSetMolotovTotalMessageCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее сообщение: {Config.MolotovTotalMessage}. Использование: css_showdamage_setmolotovtotalmessage <текст>");
            return;
        }

        string old = Config.MolotovTotalMessage;
        string value = command.GetArg(1);
        for (int i = 2; i < command.ArgCount; i++)
            value += " " + command.GetArg(i);

        Config.MolotovTotalMessage = value;
        SaveConfig();
        command.ReplyToCommand($"[ShowDamage] molotov_total_message изменён.");
    }

    private void OnSetBulletTotalEnabledCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение bullet_total_enabled: {Config.BulletTotalEnabled}. Использование: css_showdamage_setbullettotalenabled <0/1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && (value == 0 || value == 1))
        {
            int old = Config.BulletTotalEnabled;
            Config.BulletTotalEnabled = value;
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] bullet_total_enabled изменён с {old} на {value}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Используйте 0 или 1.");
        }
    }

    private void OnSetBulletAggregationTimeCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее значение bullet_aggregation_time: {Config.BulletAggregationTime.ToString(CultureInfo.InvariantCulture)}. Использование: css_showdamage_setbulletaggregationtime <0.05-5.0>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.BulletAggregationTime;
            Config.BulletAggregationTime = Math.Clamp(value, 0.05f, 5.0f);
            SaveConfig();
            command.ReplyToCommand($"[ShowDamage] bullet_aggregation_time изменён с {old.ToString(CultureInfo.InvariantCulture)} на {Config.BulletAggregationTime.ToString(CultureInfo.InvariantCulture)}.");
        }
        else
        {
            command.ReplyToCommand("[ShowDamage] Неверное значение. Введите число с точкой (например 0.3).");
        }
    }

    private void OnSetBulletTotalMessageCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[ShowDamage] Текущее сообщение: {Config.BulletTotalMessage}. Использование: css_showdamage_setbullettotalmessage <текст>");
            return;
        }

        string old = Config.BulletTotalMessage;
        string value = command.GetArg(1);
        for (int i = 2; i < command.ArgCount; i++)
            value += " " + command.GetArg(i);

        Config.BulletTotalMessage = value;
        SaveConfig();
        command.ReplyToCommand($"[ShowDamage] bullet_total_message изменён.");
    }

    private void SaveConfig()
    {
        try
        {
            string configPath = Path.Combine(Server.GameDirectory, "counterstrikesharp", "configs", "plugins", "CS2_ShowDamage", "ShowDamageConfig.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(Config, options);
            File.WriteAllText(configPath, json);
            Log(LogLevel.Debug, $"Конфигурация сохранена в {configPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ShowDamage] Ошибка сохранения конфигурации");
        }
    }

    private void ClearHudMessages()
    {
        foreach (var timer in _messageTimers.Values)
        {
            timer.Kill();
        }

        _playerMessages.Clear();
        _messageTimers.Clear();
    }

    private void ClearAllDamage()
    {
        foreach (var slotTimers in _timersByKey.Values)
        {
            foreach (var timer in slotTimers.Values)
            {
                timer.Kill();
            }
        }

        _timersByKey.Clear();
        _heGrenadeDamageInfo.Clear();
        _molotovDamageInfo.Clear();
        _molotovFirstHitTime.Clear();

        foreach (var aggregator in _bulletAggregators.Values)
        {
            if (aggregator.Timer != null)
            {
                aggregator.Timer.Kill();
            }
        }
        _bulletAggregators.Clear();

        foreach (var shotInfo in _shotInfo.Values)
        {
            if (shotInfo.Timer != null)
            {
                shotInfo.Timer.Kill();
            }
        }
        _shotInfo.Clear();

        foreach (var attackerDict in _singleTargetDamage.Values)
        {
            foreach (var targetDamage in attackerDict.Values)
            {
                if (targetDamage.Timer != null)
                {
                    targetDamage.Timer.Kill();
                }
            }
        }
        _singleTargetDamage.Clear();

        var totalDamageMessages = _playerMessages
            .Where(x => x.Value.Contains("Общий урон от") || x.Value.Contains("Общий урон:"))
            .Select(x => x.Key)
            .ToList();

        foreach (var slot in totalDamageMessages)
        {
            _playerMessages.Remove(slot);
        }
    }

    public override void Unload(bool hotReload)
    {
        ClearHudMessages();
        ClearAllDamage();
        Log(LogLevel.Information, "Плагин выгружен.");
    }
}
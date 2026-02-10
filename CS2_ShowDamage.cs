using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json.Serialization;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CS2_ShowDamage;

public class ShowDamageConfig : BasePluginConfig
{
    /// <summary>
    /// Включить/выключить плагин
    /// true - включено, false - выключено
    /// </summary>
    [JsonPropertyName("css_showdamage_enabled")]
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Длительность отображения уведомления (секунды)
    /// Диапазон: 0.1-10.0
    /// </summary>
    [JsonPropertyName("css_showdamage_duration")]
    public float NotifyDuration { get; set; } = 1.0f;
    
    /// <summary>
    /// Уровень логирования
    /// 0 - Только ошибки
    /// 1 - Ошибки и информация
    /// 2 - Ошибки, информация и отладка
    /// Диапазон: 0-2
    /// </summary>
    [JsonPropertyName("css_showdamage_log_level")]
    public int LogLevel { get; set; } = 0;
    
    /// <summary>
    /// Режим цвета для урона в HUD
    /// 1 - Динамический цвет в зависимости от урона
    /// Диапазон: 1
    /// </summary>
    [JsonPropertyName("css_showdamage_hud_color_mode")]
    public int HudColorMode { get; set; } = 1;
    
    /// <summary>
    /// Включить суммарный подсчет урона от гранат и молотов
    /// true - включено, false - выключено
    /// </summary>
    [JsonPropertyName("css_showdamage_grenade_total_enabled")]
    public bool GrenadeTotalEnabled { get; set; } = true;
    
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
    /// true - включено, false - выключено
    /// </summary>
    [JsonPropertyName("css_showdamage_bullet_total_enabled")]
    public bool BulletTotalEnabled { get; set; } = true;
    
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
    public override string ModuleVersion => "2.9";
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
        Config.NotifyDuration = Math.Clamp(Config.NotifyDuration, 0.1f, 10.0f);
        Config.LogLevel = Math.Clamp(Config.LogLevel, 0, 2);
        Config.HudColorMode = Math.Clamp(Config.HudColorMode, 1, 1);
        Config.GrenadeTotalDuration = Math.Clamp(Config.GrenadeTotalDuration, 1.0f, 10.0f);
        Config.MolotovAggregationDuration = Math.Clamp(Config.MolotovAggregationDuration, 1.0f, 15.0f);
        Config.BulletAggregationTime = Math.Clamp(Config.BulletAggregationTime, 0.05f, 5.0f);
        
        LogInfo($"Конфиг загружен: Включен={Config.Enabled}, Уровень логирования={Config.LogLevel}, Суммарный урон гранат={Config.GrenadeTotalEnabled}, Агрегация пуль={Config.BulletTotalEnabled}");
    }
    
    public override void Load(bool hotReload)
    {
        // Регистрируем команды
        AddCommand("css_showdamage_help", "Показать справку по плагину ShowDamage", OnHelpCommand);
        AddCommand("css_showdamage_settings", "Показать текущие настройки ShowDamage", OnSettingsCommand);
        AddCommand("css_showdamage_reload", "Перезагрузить конфигурацию ShowDamage", OnReloadCommand);
        AddCommand("css_showdamage_cleardamage", "Очистить суммарный урон от гранат, молотов и пуль", OnClearDamageCommand);
        AddCommand("css_showdamage_toggle", "Включить/выключить плагин ShowDamage", OnToggleCommand);
        
        // Регистрируем обработчик событий
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Post);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire, HookMode.Post);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Post);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Post);
        
        // Регистрируем тик для отображения HUD сообщений
        RegisterListener<Listeners.OnTick>(OnTick);
        
        // Выводим информацию о конфигурации
        PrintConVarInfo();
        
        LogInfo($"Плагин v{ModuleVersion} успешно загружен (Включен: {Config.Enabled}, Уровень логирования: {Config.LogLevel})");
    }
    
    private void PrintConVarInfo()
    {
        Console.WriteLine("===============================================");
        Console.WriteLine("[ShowDamage] Plugin successfully loaded!");
        Console.WriteLine($"[ShowDamage] Version: {ModuleVersion}");
        Console.WriteLine($"[ShowDamage] Minimum API Version: 362");
        Console.WriteLine("[ShowDamage] Configuration file created automatically!");
        Console.WriteLine("[ShowDamage] Current settings:");
        Console.WriteLine($"[ShowDamage]   css_showdamage_enabled = {Config.Enabled}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_duration = {Config.NotifyDuration}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_log_level = {Config.LogLevel}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_hud_color_mode = {Config.HudColorMode}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_grenade_total_enabled = {Config.GrenadeTotalEnabled}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_grenade_total_duration = {Config.GrenadeTotalDuration}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_molotov_aggregation_duration = {Config.MolotovAggregationDuration}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_bullet_total_enabled = {Config.BulletTotalEnabled}");
        Console.WriteLine($"[ShowDamage]   css_showdamage_bullet_aggregation_time = {Config.BulletAggregationTime}");
        Console.WriteLine("[ShowDamage] Console commands:");
        Console.WriteLine("[ShowDamage]   css_showdamage_help - Show plugin help");
        Console.WriteLine("[ShowDamage]   css_showdamage_settings - Show current settings");
        Console.WriteLine("[ShowDamage]   css_showdamage_reload - Reload configuration");
        Console.WriteLine("[ShowDamage]   css_showdamage_cleardamage - Clear all damage totals");
        Console.WriteLine("[ShowDamage]   css_showdamage_toggle - Toggle plugin on/off");
        Console.WriteLine("===============================================");
    }
    
    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        try
        {
            if (!Config.Enabled)
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
            
            LogDebug($"Урон: {attacker.PlayerName} -> {victim.PlayerName}: {damage} HP (оружие={weapon}, голова={isHeadshot}, убийство={isKill}, здоровье={health})");
            
            // Проверяем, является ли оружие HE-гранатой или молотовым
            if (Config.GrenadeTotalEnabled && IsDamageGrenade(weapon, out var grenadeType))
            {
                // Показываем мгновенный урон для гранат и молотов
                ShowSingleDamage(attacker, damage, health, isHeadshot, isKill);
                
                // Затем агрегируем для суммарного урона
                ProcessGrenadeDamage(attacker, victim, damage, weapon, grenadeType);
            }
            else if (Config.BulletTotalEnabled)
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
            LogError($"Ошибка в OnPlayerHurt: {ex.Message}");
        }
        
        return HookResult.Continue;
    }
    
    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        try
        {
            if (!Config.Enabled)
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
            
            LogDebug($"Новый выстрел: {attacker.PlayerName} - {weapon} (ID: {shotId})");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка в OnWeaponFire: {ex.Message}");
        }
        
        return HookResult.Continue;
    }
    
    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        try
        {
            if (!Config.Enabled)
                return HookResult.Continue;
            
            var victim = @event.Userid;
            var attacker = @event.Attacker;
            
            if (victim == null || !victim.IsValid || attacker == null || !attacker.IsValid)
                return HookResult.Continue;
                
            if (attacker == victim || attacker.TeamNum == victim.TeamNum)
                return HookResult.Continue;
            
            var weapon = @event.Weapon;
            var isHeadshot = @event.Headshot;
            
            LogDebug($"Смерть: {attacker.PlayerName} -> {victim.PlayerName} (оружие={weapon}, голова={isHeadshot})");
            
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
                LogDebug($"Убийство с одного выстрела: {attacker.PlayerName} -> {victim.PlayerName}");
                
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
            LogError($"Ошибка в OnPlayerDeath: {ex.Message}");
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
            
            LogDebug($"Показано сообщение об убийстве: {attacker.PlayerName} - {damage} HP [УБИТ]");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка в ShowKillMessage: {ex.Message}");
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
                damageDict[attackerSlot] = new GrenadeDamageInfo { 
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
                    LogDebug($"Запущен таймер молотова для {attacker.PlayerName} на {Config.MolotovAggregationDuration} секунд");
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
            LogDebug($"{weaponNameRU}: {attacker.PlayerName} -> {victim.PlayerName}: {damage} HP (общий={grenadeInfo.TotalDamage}, игроков={grenadeInfo.VictimSlots.Count})");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка в ProcessGrenadeDamage: {ex.Message}");
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
                    LogDebug($"Обнаружено пробитие: {attacker.PlayerName} - выстрел #{shot.ShotId}, хит #{shot.HitCount}");
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
                    aggregator = new BulletDamageAggregator { 
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
            
            LogDebug($"Пуля: {attacker.PlayerName} -> {victim.PlayerName}: {damage} HP (убийство={isKill}, пробитие={aggregator.IsPenetrationShot}, выстрел={shotId}, суммарный по цели={aggregator.CurrentTargetTotalDamage}, общий={aggregator.TotalDamage}, целей={aggregator.VictimSlots.Count}, убито={aggregator.KilledSlots.Count})");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка в ProcessBulletDamage: {ex.Message}");
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
            LogError($"Ошибка в UpdateSingleTargetDamage: {ex.Message}");
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
            LogError($"Ошибка в ShowSingleTargetDamage: {ex.Message}");
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
            LogError($"Ошибка в ShowSingleDamage: {ex.Message}");
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
                LogDebug($"Игрок со слотом {attackerSlot} не найден при попытке показать общий урон от {grenadeType}");
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
            LogInfo($"Суммарный урон {weaponNameRU}: {attacker.PlayerName} - {grenadeInfo.TotalDamage} HP (поражено {grenadeInfo.VictimSlots.Count} игроков)");
        }
        catch (Exception ex)
        {
            LogError($"Ошибка в ShowTotalGrenadeDamage: {ex.Message}");
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
                
                LogInfo($"Суммарный урон пуль{penetrationNote}: {attacker.PlayerName} - {aggregator.TotalDamage} HP (поражено {aggregator.VictimSlots.Count} игроков, убито {aggregator.KilledSlots.Count})");
            }
            
            _bulletAggregators.Remove(attackerSlot);
        }
        catch (Exception ex)
        {
            LogError($"Ошибка в ShowTotalBulletDamage: {ex.Message}");
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
            if (!Config.Enabled)
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
            LogError($"Ошибка в OnTick: {ex.Message}");
        }
    }
    
    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        try
        {
            if (!Config.Enabled)
                return HookResult.Continue;
            
            ClearHudMessages();
            ClearAllDamage();
        }
        catch (Exception ex)
        {
            LogError($"Ошибка в OnRoundEnd: {ex.Message}");
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
            LogError($"Ошибка в CleanupPlayerData: {ex.Message}");
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
            LogError($"Ошибка в OnPlayerDisconnect: {ex.Message}");
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
    
    // Команды остаются без изменений
    private void OnHelpCommand(CCSPlayerController? player, CommandInfo command)
    {
        string helpMessage = """
            ================================================
            SHOWDAMAGE PLUGIN HELP
            ================================================
            DESCRIPTION:
              Shows damage dealt to players in HUD display.
              Includes total damage calculation for HE grenades, molotovs and bullets.

            CONFIGURATION FILE:
              addons/counterstrikesharp/configs/plugins/CS2_ShowDamage/ShowDamageConfig.json

            CONSOLE COMMANDS:
              css_showdamage_help - Show this help message
              css_showdamage_settings - Show current plugin settings
              css_showdamage_reload - Reload configuration from file
              css_showdamage_cleardamage - Clear all damage totals
              css_showdamage_toggle - Toggle plugin on/off

            CONFIGURATION VARIABLES:
              css_showdamage_enabled (true/false) - Enable/disable plugin
              css_showdamage_duration (0.1-10.0) - HUD display duration in seconds
              css_showdamage_log_level (0-2) - Logging level (0=Error,1=Info,2=Debug)
              css_showdamage_hud_color_mode (1) - HUD color mode (1=dynamic by damage)
              css_showdamage_grenade_total_enabled (true/false) - Enable grenade damage total
              css_showdamage_grenade_total_duration (1.0-10.0) - Grenade total display duration
              css_showdamage_molotov_aggregation_duration (1.0-15.0) - Molotov aggregation duration
              css_showdamage_grenade_total_message (string) - HE grenade total message
              css_showdamage_molotov_total_message (string) - Molotov total message
              css_showdamage_bullet_total_enabled (true/false) - Enable bullet damage total
              css_showdamage_bullet_aggregation_time (0.05-5.0) - Bullet aggregation time
              css_showdamage_bullet_total_message (string) - Bullet total message format
            ================================================
            """;
        
        if (player != null)
        {
            player.PrintToConsole(helpMessage);
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}Check console for help");
        }
        else
        {
            Console.WriteLine(helpMessage);
        }
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
            SHOWDAMAGE v{ModuleVersion} - CURRENT SETTINGS
            ================================================
            Plugin Enabled: {Config.Enabled}
            HUD Duration: {Config.NotifyDuration} seconds
            Log Level: {Config.LogLevel} ({(Config.LogLevel == 0 ? "Error" : Config.LogLevel == 1 ? "Info" : "Debug")})
            Grenade Total: {Config.GrenadeTotalEnabled}, Duration={Config.GrenadeTotalDuration}s
            Molotov Aggregation: {Config.MolotovAggregationDuration}s
            HE Grenade Message: {Config.GrenadeTotalMessage}
            Molotov Message: {Config.MolotovTotalMessage}
            Bullet Total: {Config.BulletTotalEnabled}, Aggregation={Config.BulletAggregationTime}s
            Bullet Message: {Config.BulletTotalMessage}
            Active HUD Messages: {_playerMessages.Count}
            Active HE Grenades: {activeHEGrenades}
            Active Molotovs: {activeMolotovs}
            Active Bullets: {activeBullets}
            Single Target Entries: {singleTargetEntries}
            Total Grenade Damage: {totalGrenadeDamage} HP ({totalGrenadePlayers} players)
            Total Bullet Damage: {totalBulletDamage} HP ({totalBulletPlayers} players, killed: {totalKilled})
            ================================================
            """;
        
        if (player != null)
        {
            player.PrintToConsole(settingsMessage);
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}Plugin: {Config.Enabled}, LogLevel: {Config.LogLevel}");
        }
        else
        {
            Console.WriteLine(settingsMessage);
        }
    }
    
    private void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        string message = "Configuration reloaded successfully!";
        Console.WriteLine($"[ShowDamage] {message}");
        
        ClearHudMessages();
        ClearAllDamage();
        
        if (player != null)
        {
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}{message}");
        }
    }
    
    private void OnClearDamageCommand(CCSPlayerController? player, CommandInfo command)
    {
        int clearedCount = _heGrenadeDamageInfo.Count + _molotovDamageInfo.Count + _bulletAggregators.Count;
        ClearAllDamage();
        
        string message = $"Cleared {clearedCount} damage totals (grenades/molotovs/bullets).";
        Console.WriteLine($"[ShowDamage] {message}");
        
        if (player != null)
        {
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}{message}");
        }
        else
        {
            command.ReplyToCommand($"[ShowDamage] {message}");
        }
    }
    
    private void OnToggleCommand(CCSPlayerController? player, CommandInfo command)
    {
        Config.Enabled = !Config.Enabled;
        
        string message = $"Plugin ShowDamage is now {(Config.Enabled ? "ENABLED" : "DISABLED")}.";
        
        if (!Config.Enabled)
        {
            ClearHudMessages();
            ClearAllDamage();
        }
        
        Console.WriteLine($"[ShowDamage] {message}");
        
        if (player != null)
        {
            player.PrintToChat($" {ChatColors.Green}[ShowDamage] {ChatColors.White}{message}");
        }
        else
        {
            command.ReplyToCommand($"[ShowDamage] {message}");
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
        LogInfo("Plugin unloaded");
    }
    
    private void LogError(string message)
    {
        Console.WriteLine($"[ShowDamage ERROR] {DateTime.Now:HH:mm:ss} - {message}");
    }
    
    private void LogInfo(string message)
    {
        if (Config.LogLevel >= 1)
            Console.WriteLine($"[ShowDamage INFO] {DateTime.Now:HH:mm:ss} - {message}");
    }
    
    private void LogDebug(string message)
    {
        if (Config.LogLevel >= 2)
            Console.WriteLine($"[ShowDamage DEBUG] {DateTime.Now:HH:mm:ss} - {message}");
    }
}
# CS2_ShowDamage
Плагин отображает нанесённый урон в центре экрана (HUD) для каждого игрока. Поддерживает суммирование урона от гранат (HE), молотовых коктейлей и пуль (включая пробития). Учитываются хедшоты и убийства. Информация показывается в цвете в зависимости от величины урона.

# https://www.youtube.com/watch?v=yt4QmyRjSuk

# Требования
```
CounterStrikeSharp API версии 362 или выше
.NET 8.0 Runtime
```

# Конфигурационные параметры
```
css_showdamage_enabled <0/1>, def.=1 – Включение/выключение плагина.
css_showdamage_duration <0.1-10.0>, def.=1.0 – Длительность отображения уведомления в HUD (секунды).
css_showdamage_log_level <0-5>, def.=4 – Уровень логирования (0-Trace,1-Debug,2-Info,3-Warning,4-Error,5-Critical).
css_showdamage_hud_color_mode <1>, def.=1 – Режим цвета для урона в HUD (только 1 – динамический цвет в зависимости от урона).
css_showdamage_grenade_total_enabled <0/1>, def.=1 – Включить суммарный подсчет урона от гранат и молотовых (показывается общий урон и количество поражённых игроков).
css_showdamage_grenade_total_duration <1.0-10.0>, def.=3.0 – Длительность отображения суммарного урона от гранат (секунды).
css_showdamage_molotov_aggregation_duration <1.0-15.0>, def.=7.0 – Время агрегации урона от молотового коктейля (секунды). Урон суммируется за этот период и показывается общий результат.
css_showdamage_grenade_total_message (строка), def.="Общий урон от гранаты: <font color='red'>{0} HP</font> (поражено: <font color='green'>{1} игроков</font>)" – Шаблон сообщения для суммарного урона от HE-гранаты. {0} – общий урон, {1} – количество поражённых игроков.
css_showdamage_molotov_total_message (строка), def.="Общий урон от молотового: <font color='red'>{0} HP</font> (поражено: <font color='green'>{1} игроков</font>)" – Шаблон сообщения для суммарного урона от молотового коктейля.
css_showdamage_bullet_total_enabled <0/1>, def.=1 – Включить общий подсчет урона от пуль за выстрел (включая пробития).
css_showdamage_bullet_aggregation_time <0.05-5.0>, def.=0.3 – Время агрегации пуль для общего подсчета (секунды). Урон от нескольких попаданий в рамках одного выстрела (например, при пробитии) суммируется и показывается единым сообщением.
css_showdamage_bullet_total_message (строка), def.="Общий урон: <font color='red'>{0} HP</font> (поражено: <font color='green'>{1} игроков</font>)" – Шаблон сообщения для суммарного урона от пуль.
```

# Консольные команды
```
css_showdamage_help – Показать подробную справку по плагину.
css_showdamage_settings – Показать текущие настройки плагина и активные данные (количество записей об уроне).
css_showdamage_test – Отправить тестовое сообщение в HUD и вывести информацию о настройках в чат (доступно только игроку).
css_showdamage_reload – Перезагрузить конфигурацию из файла и очистить все накопленные данные об уроне.
css_showdamage_cleardamage – Очистить все накопленные данные об уроне (гранаты, пули, агрегаторы).
css_showdamage_toggle <0/1> – Включить (1) или выключить (0) плагин (аналог css_showdamage_setenabled).
css_showdamage_setenabled <0/1> – Установить значение css_showdamage_enabled.
css_showdamage_setnotifyduration <0.1-10.0> – Установить css_showdamage_duration.
css_showdamage_setloglevel <0-5> – Установить css_showdamage_log_level.
css_showdamage_sethudcolormode <1> – Установить css_showdamage_hud_color_mode (только 1).
css_showdamage_setgrenadetotalenabled <0/1> – Установить css_showdamage_grenade_total_enabled.
css_showdamage_setgrenadetotalduration <1.0-10.0> – Установить css_showdamage_grenade_total_duration.
css_showdamage_setmolotovaggregationduration <1.0-15.0> – Установить css_showdamage_molotov_aggregation_duration.
css_showdamage_setgrenadetotalmessage <текст> – Установить шаблон сообщения для HE-гранаты (css_showdamage_grenade_total_message). Можно использовать несколько слов.
css_showdamage_setmolotovtotalmessage <текст> – Установить шаблон сообщения для молотова (css_showdamage_molotov_total_message).
css_showdamage_setbullettotalenabled <0/1> – Установить css_showdamage_bullet_total_enabled.
css_showdamage_setbulletaggregationtime <0.05-5.0> – Установить css_showdamage_bullet_aggregation_time.
css_showdamage_setbullettotalmessage <текст> – Установить шаблон сообщения для пуль (css_showdamage_bullet_total_message).
```

# ЭТОТ ПЛАГИН ФОРК ЭТИХ ПЛАГИНОВ:

https://github.com/ABKAM2023/CS2ShowDamage

https://hlmod.net/resources/cs2-show-damage.4514/

https://github.com/abnerfs/cs2-showdamage


# MortalControlMyDestiny
 活侠传mod - 知天命


本MOD使用BepInEx开发。
https://github.com/BepInEx/BepInEx
所需外部库请自行参阅BepInEx使用方法


-0.0.3更新
修复一些异常问题；升级了BepInEx内核，以兼容别的mod


-1.0.0更新
支持显示事件名称（通过抓取官方的开发备注），可以在配置里关闭该功能以免剧透
支持配置文件


-1.0.1更新
所有心上人id显示为人名
过长的分支条件自动换行


-1.0.2更新（20240620）
当检查点刷新时，显示在屏幕上


【功能1. 天命选择】
在天命（轮盘掷骰）开始Roll点前，可以通过直接点击选项进行选择。
一旦轮盘开始转动，便无法再进行该项操作。


【功能2. 分支提醒】
当玩家在事件中遭遇分支检测时，显示分支条件。
绿色表示条件通过，红色则是不满足。
有多个分支条件则表示在这里会有多个事件可以触发。


【功能3. 检查点提醒】
当玩家在事件中刷新了检查点，显示检查点的情况
（例如可以第一时间知道是否救下大师兄，但因为我已经覆盖了那个时间节点的存档，所以本人暂时无法验证）

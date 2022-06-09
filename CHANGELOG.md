# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### [1.2.3] - 2022-05-18

### Added

- 新增VirtualGameObject组件的PriorityRivise（优先级偏差值）功能。
- SoundSFX组件的波形图新增FadeInOut的图形显示。
- EventLogWindow在EventLog页面新增了点击显示相关联内容的功能。
- 新增EventLogWindow的EventLogPage的折叠内容功能。出于性能考虑，现在EventLogPage不再显示全部事件记录，而是通过滚动方式读取列表内容。
- EventLogWindow的EventLogPage新增到顶部或底部的按钮。
- 新增EventLogWindow的InstancePage中Switch的信息展示。
- EventLog窗口中InstancePage适配搜索功能。

### Changed

- 修改了优先级限制计算的算法。

### Fixed

- 修复LimitPlay功能没有正确使用父级ActorMixer的数据。
- 修复在初次打开EventLogWindow查看InstancePage页面时没有获取到数据列表的问题。
- 修复在不执行初始化时会将数据清除导致无法加载配置文件的问题。
- 修复在Manager没有加载数据时EventLogWIndow中查看Parameter等数据会报错的问题。

## [1.2.2] - 2022-03-24

### Added

- 在AudioComponent的3DSetting页面，新增优先级随距离衰减的功能。
- 新增缓存池缓冲已创建的GameObject，减少性能消耗。AudioEditorManager新增**ClearCached**方法清除缓存。

### Changed

- 发声体的位置跟随改为FixUpdate执行，避免发声体位置移动过快时导致3D效果会有偏差的问题。

## [1.2.1] - 2022-01-28

### Changed

- 现在WorkUnit可以成为ActorMixer的子组件。

### Fixed

- 修复在获取workUnit的基础类型时可能会报错的问题。

## [1.2.0] - 2022-01-14

### Added

- 新增ProjectSetting中的performanceLevel选项，数值越高，越强调质量，每帧处理的音频组件内容会减少。
- 新增了AudioEditorManager的**RegisterAudioListener**的API，用于手动向插件注册AudioListener以完成3D相关功能。
- 新增AEGameObjectComponent组件，用于记录Switch和GameParameter的局部值数据。
- EventRefecrence中**PostEvent**新增triggerObject参数，triggerObject和TargetObject的概念进一步区分，事件由TriggerObject触发，动作作用于TargetObject。

- 新增VirtualGameObjectGroup和VirtualGameObject组件。

### Changed

- AudioComponent的OtherSetting页面中，limitPlayNumber可为0值，代表无限制。

### Fixed

- 修复波形图无法正常播放的问题。

- 修复EventLogWindow中SetGameParameter动作中没有正确显示parameter名称的问题。

- 修复EventLogWindow中动作触发信息在编辑器刷新后会丢失数据的问题。
- 现在Switch修改时SwitchGroup会正确同步其触发者的局部值。

## [1.1.3] - 2021-12-28

### Added

- 在ProjectSettings中新增Disable选项。

### Changed

- 当加载AudioClip失败时提示组件名和id。
- 改善EventLogWindow的EventLog页面在事件高频次触发下卡顿的情况。
- 现在已销毁的GameObject所依附的Parameter信息不再显示在EventLogWindow中Parameter窗口中。
- 改善波形图读取时候造成的卡顿情况。

## [1.1.2] - 2021-12-20

### Added

- 新增当事件动作无法被覆盖时增加提示。
- 新增EventLogWindow中GameParameter的展示。

### Changed

- GameParameter中的GameObjectValue储存为实际值而非归一化值，提供新的API来访问获取其归一化值。

### Fixed

- 修复其他事件动作可以被覆盖为SetParameter的问题。

## [1.1.1] - 2021-12-17

### Added:

- LimitPlayNumber现在区分为两个值，一个指示限制在同一个GameObject中触发的数量，一个指示限制在全局中触发的数量。

### Changed

- 优先级（priority）的机制进行修改，现在优先级的最终输出结果为当前生效的priority基础值 + 所有相关RTPC曲线中的数值。
- Limit Play机制进行更新，当达到最大限制时，如果新触发的AEAudioComponent组件的优先级高于已经生成的其中一项时，依旧会进行生成，这会突破最大限制。

### Fixed

- 修复在PlayableInstance生成时没有同步priority的问题。

## [1.0.0] - 2021-12-10

- Initial Release
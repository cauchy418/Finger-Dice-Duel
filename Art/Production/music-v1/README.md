# 三首配乐与自适应鼓层

来源为玩家提供的River at Dusk、River at Battlefield、Dawn of Steel三份WAV。`source`中的FLAC与原WAV解码后的16位、48kHz双声道PCM逐样本相同；不替换或修改下载目录的原文件。

[manifest.json](manifest.json)保存原WAV与FLAC的SHA256、采样级循环边界、回接处理、音量和十份运行OGG的SHA256。[analysis.json](analysis.json)保留候选循环、自动估计曲速及节拍位置。新增鼓层为合成的低鼓、通鼓和轻打击，不声称是原曲分轨；导出使用固定种子418，与游戏随机数无关。

在项目根目录安装[离线工具依赖](../../../Tools/Audio/requirements.txt)，运行[analyze_music.py](../../../Tools/Audio/analyze_music.py)分析，再运行[export_music.py](../../../Tools/Audio/export_music.py)导出。优先读下载目录原WAV，缺失时读已留档FLAC；导出可直接使用现有分析结果。FFmpeg通过imageio-ffmpeg提供，不依赖系统PATH。运行资产写入Assets/Resources/Audio/Music；在编辑器中运行[导入配置](../../Integration/music-v1/configure-import.csx)恢复流式、立体声及采样率保留设置。

首次播放使用intro，随后播放loop；两个战斗曲目另各有等长的drums_intro和drums_loop，总计十份OGG。回接平滑使用循环起点前的音频，保留完整节拍长度。FLAC只作为复现源，不进入游戏资源加载。

[播放规则与循环时间](../../../Docs/music-and-audio.md) · [验收记录](../../Integration/music-v1/README.md)

# QuantBox.DataSimulator 数据模拟器

##介绍
很多客户有自己的历史数据文件或数据源，但每次将历史数据导入到OpenQuant中是一件很麻烦的事情，如果能直接读取数据文件进行回测不就很方便了。所以我们在这提供了一种直接读取数据进行回测的方法。<br/>

##依赖库
1. 7z库：SevenZipSharp.dll、7z64.dll、7z.dll
2. QuantBox.Data项目：QuantBox.Data.Serializer.dll，protobuf-net.dll [https://github.com/QuantBox/QuantBox.Data](https://github.com/QuantBox/QuantBox.Data "QuantBox.Data")

## 使用方法
1. Data.zip是测试用的数据，先解压
2. 将QuantBox.DataSimulator.dll以及上面的几个依赖库复制到OpenQuant2014的安装目录下
3. 给项目添加QuantBox.DataSimulator.dll引用
4. 在用来回测或优化的项目中的Scenario中添加如下
<pre><code>
using QuantBox;
</code></pre>
<pre><code>
public Backtest(Framework framework)
	: base(framework)
{
	// 修改回测用的数据模拟器
	FileDataSimulator pvd = (FileDataSimulator)framework.ProviderManager.GetProvider(50);
	if(pvd == null)
	{
		pvd = new FileDataSimulator(framework);
		framework.ProviderManager.AddProvider(pvd);
	}
	// 请修改成你自己的数据目录
	pvd.DataPath = @"d:\wukan\Desktop\Data";
	pvd.SubscribeExternData = true;
	pvd.SubscribeAsk = true;
	pvd.SubscribeBid = true;
	framework.ProviderManager.DataSimulator = pvd;
	
	// 临时添加合约用于回测，不保存
	BacktestInstrumentServer.AddDirectoryInstrument(framework,pvd.DataPath);
}</code></pre>

## 解释
1. GetProvider(50)是因为FileDataSimulator中默认设的是50
2. DataPath是数据目录路径，里面的文件夹名是合约名如IF1509，文件夹中的文件中的日期就是数据的交易日
3. 很多时候合约列表中添加合约太麻烦，自己又是只是回测的时候临时用一下，用BacktestInstrumentServer.AddDirectoryInstrument可以根据指定文件夹下的子文件夹创建对应的合约

## 其它
1. 有兴趣的可以改成一个读取通达信行情文件的版本
2. 读取文本和数据库也是可以实现的





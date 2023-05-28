# MonoTimer

这是一个基于MonoBehaviour的计时系统， 支持真实时间和受TimeScale影响时间的计时。

原理就是加入任务时计算完成时的时间，根据这个时间进行排序，数据结构用的是SortSet。然后在Update循环中只等待第一个任务，完成后再等第二个任务。

使用方法是new出一个DelaySystem或者IntervalSystem，调用AddXXXTask就行了，如果想取消，就调用task的token的Cancel就行。也可以直接用DelaySystem里的静态的GlobalDelay，但是在生命周期外不能直接引用，最好是get拿到，不然会报错。

只能支持少量任务，一是排序消耗性能，二是因为底层是Set，所以遇到相同时间的任务时会增加一个小误差来避开。一开始没想取消的功能，使用了Set和多播委托实现，加入取消功能后无法实现分离出对应的委托，只能改为用误差来实现分隔。

Set里套List应该也可以，懒得改了。

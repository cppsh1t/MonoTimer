# MonoTimer

这是一个基于MonoBehaviour的计时系统

支持真实时间和受TimeScale影响时间的计时。
原理就是加入任务时计算完成时的时间，根据这个时间进行排序，数据结构用的是SortSet。然后在Update循环中只等待第一个任务，完成后再等第二个任务。

 使用方法是new出一个计时系统，调用AddDelayTask就行了，如果想取消，就调用task的token的Cancel就行。也可以直接用DelaySystem里的静态的GlobalDelay，但是在生命周期外不能直接引用，最好是get拿到，不然会报错。

不足之处就是任务量多的时候排序会很消耗性能，等着我写一个结构分组一下DelayTask，手动控制粒度。

还有就是interval和注释还没写，下次再说把。

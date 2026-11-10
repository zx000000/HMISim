namespace HMISim.Common;

/// <summary>
/// 全项目统一日志抽象（规范 v0.2 第 7 节）。业务代码只依赖此接口，不直引具体日志框架。
/// </summary>
public interface IAppLogger
{
    /// <summary>常规信息（禁止在调度热循环内逐帧调用）。</summary>
    void Info(string message);

    /// <summary>警告：可恢复异常、质量降级等。</summary>
    void Warn(string message);

    /// <summary>错误：必须带上下文（设备/变量/错误码），异常可为空。</summary>
    void Error(string message, Exception? exception = null);
}

/// <summary>空日志实现，未注入日志时的默认选择。</summary>
public sealed class NullLogger : IAppLogger
{
    /// <summary>共享单例。</summary>
    public static readonly NullLogger Instance = new();

    private NullLogger()
    {
    }

    /// <inheritdoc />
    public void Info(string message)
    {
    }

    /// <inheritdoc />
    public void Warn(string message)
    {
    }

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null)
    {
    }
}

using System.Drawing;
using System.Net.Sockets;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ;

public class RabbitMQPersistentConnection : IDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private IConnection connection;
    private object _lockObject = new object();
    private readonly int retryCount;
    private bool _dispoed;


    public RabbitMQPersistentConnection(IConnectionFactory connectionFactory,
        int connection, int retryCount = 5)
    {
        _connectionFactory = connectionFactory;
        connection = connection;
    }


    public bool IsConnection => connection != null && connection.IsOpen;

    public IModel CreateModel()
    {
        return connection.CreateModel();
    }

    public void Dispose()
    {
        _dispoed = true;
        connection.Dispose();
    }

    public bool TryConnect()
    {
        lock (_lockObject)
        {
            var policy = Policy.Handle<SocketException>().Or<BrokerUnreachableException>().WaitAndRetry(retryCount,
                retryAttemp => TimeSpan.FromSeconds(Math.Pow(2, retryAttemp)), (ex, time) => { }
            );

            policy.Execute(() => { connection = _connectionFactory.CreateConnection(); });

            if (IsConnection)
            {
                connection.ConnectionShutdown += Connection_ConnectionShutdown;
                connection.CallbackException += Connection_ConnectionCallBack;
                connection.ConnectionBlocked += Connection_ConnectionBlocked;
                //log

                return true;
            }

            return false;
        }
    }

    private void Connection_ConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        //log
        if (_dispoed) return;
            TryConnect();
    }

    private void Connection_ConnectionCallBack(object? sender, CallbackExceptionEventArgs e)
    {
        //log
        if (_dispoed) return;
        TryConnect();
    }

    private void Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
    { 
        //log,
        if (_dispoed) return;
        TryConnect();
    }
}
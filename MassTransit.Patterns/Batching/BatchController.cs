namespace MassTransit.Patterns.Batching
{
    using System;
    using System.Collections.Generic;
    using ServiceBus;

    public class BatchController<T, K> :
        IMessage where T : BatchMessage<K>
    {
        private readonly Dictionary<K, IBatchContext<T, K>> _contexts = new Dictionary<K, IBatchContext<T, K>>();
        private readonly BatchControllerHandler<T, K> _handler;
        private readonly object _lockContext = new object();
        private readonly TimeSpan _timeout;

        public BatchController(BatchControllerHandler<T, K> handler)
        {
            _handler = handler;
            _timeout = TimeSpan.FromMinutes(30);
        }

        public BatchController(BatchControllerHandler<T, K> handler, TimeSpan timeout)
        {
            _handler = handler;
            _timeout = timeout;
        }

        public void HandleMessage(IMessageContext<T> context)
        {
            K batchId = context.Message.BatchId;

            IBatchContext<T, K> batchContext;

            bool invokeHandler = false;

            lock (_lockContext)
            {
				if (!_contexts.ContainsKey(batchId))
				{
					batchContext = new BatchContext<T, K>(context, batchId, _timeout);

					_contexts.Add(batchId, batchContext);

					invokeHandler = true;
				}
				else
				{
					batchContext = _contexts[batchId];
				}

            }

            // push this message to the context, releasing the enumerator
            batchContext.Enqueue(context.Message);

            if (invokeHandler)
            {
                _handler(batchContext);
            }
        }

        private IBatchContext<T, K> GetBatchContext(K batchId, IMessageContext<T> context)
        {
            IBatchContext<T, K> result;

            if (_contexts.ContainsKey(batchId))
            {
                result = _contexts[batchId];
            }
            else
            {
                // we don't have a context for this one yet, so create one
                result = new BatchContext<T, K>(context, batchId, _timeout);
                _contexts.Add(batchId, result);
            }

            return result;
        }
    }

    public delegate void BatchControllerHandler<T, K>(IBatchContext<T, K> context) where T : BatchMessage<K>;
}
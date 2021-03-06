﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap.Internal.Extensions;

namespace CliWrap
{
    /// <summary>
    /// Abstraction that represents an inwards-facing pipe.
    /// </summary>
    public abstract partial class PipeSource
    {
        /// <summary>
        /// Copies the binary content pushed to the pipe into the destination stream.
        /// </summary>
        public abstract Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default);
    }

    public partial class PipeSource
    {
        /// <summary>
        /// Pipe source that does not provide any data.
        /// Logical equivalent to <code>/dev/null</code>.
        /// </summary>
        public static PipeSource Null { get; } = new NullPipeSource();

        /// <summary>
        /// Creates a pipe source that reads from a stream.
        /// </summary>
        public static PipeSource FromStream(Stream stream, bool autoFlush) => new StreamPipeSource(stream, autoFlush);

        /// <summary>
        /// Creates a pipe source that reads from a stream.
        /// </summary>
        // TODO: change to optional argument when breaking changes are ok
        public static PipeSource FromStream(Stream stream) => FromStream(stream, true);

        /// <summary>
        /// Creates a pipe source that reads from in-memory data.
        /// </summary>
        public static PipeSource FromBytes(byte[] data) => new InMemoryPipeSource(data);

        /// <summary>
        /// Creates a pipe source that reads from a string.
        /// </summary>
        public static PipeSource FromString(string str, Encoding encoding) => FromBytes(encoding.GetBytes(str));

        /// <summary>
        /// Creates a pipe source that reads from a string.
        /// Uses <see cref="Console.InputEncoding"/> to encode the string into byte stream.
        /// </summary>
        public static PipeSource FromString(string str) => FromString(str, Console.InputEncoding);

        /// <summary>
        /// Creates a pipe source that reads from standard output of a command.
        /// </summary>
        public static PipeSource FromCommand(Command command) => new CommandPipeSource(command);
    }

    internal class NullPipeSource : PipeSource
    {
        public override Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            !cancellationToken.IsCancellationRequested
                ? Task.CompletedTask
                : Task.FromCanceled(cancellationToken);
    }

    internal class StreamPipeSource : PipeSource
    {
        private readonly Stream _stream;
        private readonly bool _autoFlush;

        public StreamPipeSource(Stream stream, bool autoFlush)
        {
            _stream = stream;
            _autoFlush = autoFlush;
        }

        public override async Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            await _stream.CopyToAsync(destination, _autoFlush, cancellationToken);
    }

    internal class InMemoryPipeSource : PipeSource
    {
        private readonly byte[] _data;

        public InMemoryPipeSource(byte[] data) => _data = data;

        public override async Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            await destination.WriteAsync(_data, cancellationToken);
    }

    internal class CommandPipeSource : PipeSource
    {
        private readonly Command _command;

        public CommandPipeSource(Command command) => _command = command;

        public override async Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            // Removing `.Task` here breaks a few tests in release mode on .NET5.
            // See: https://github.com/Tyrrrz/CliWrap/issues/97
            // Likely an issue with ConfigureAwait.Fody, so may potentially get fixed with a future package update.
            await _command.WithStandardOutputPipe(PipeTarget.ToStream(destination)).ExecuteAsync(cancellationToken).Task;
    }
}
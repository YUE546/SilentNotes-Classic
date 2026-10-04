using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace SilentNotes.Crypto.SymmetricEncryption;

public class BouncyCastleXChaCha20 : ISymmetricEncryptionAlgorithm
{
	private static class ChaCha20Base
	{
		public const int BLOCK_SIZE_IN_INTS = 16;

		public static uint[] SIGMA = new uint[4] { 1634760805u, 857760878u, 2036477234u, 1797285236u };

		public static void HChaCha20(System.Span<byte> subKey, System.ReadOnlySpan<byte> key, System.ReadOnlySpan<byte> nonce)
		{
			System.Span<uint> span = stackalloc uint[16];
			HChaCha20InitialState(span, key, nonce);
			ShuffleState(span);
			span[4] = span[12];
			span[5] = span[13];
			span[6] = span[14];
			span[7] = span[15];
			ArrayUtils.StoreArray8UInt32LittleEndian(subKey, 0, (span));
		}

		private static void HChaCha20InitialState(System.Span<uint> state, System.ReadOnlySpan<byte> key, System.ReadOnlySpan<byte> nonce)
		{
			SetSigma(state);
			SetKey(state, key);
			state[12] = ArrayUtils.LoadUInt32LittleEndian(nonce, 0);
			state[13] = ArrayUtils.LoadUInt32LittleEndian(nonce, 4);
			state[14] = ArrayUtils.LoadUInt32LittleEndian(nonce, 8);
			state[15] = ArrayUtils.LoadUInt32LittleEndian(nonce, 12);
		}

		private static void ShuffleState(System.Span<uint> state)
		{
			for (int i = 0; i < 10; i++)
			{
				QuarterRound(ref state[0], ref state[4], ref state[8], ref state[12]);
				QuarterRound(ref state[1], ref state[5], ref state[9], ref state[13]);
				QuarterRound(ref state[2], ref state[6], ref state[10], ref state[14]);
				QuarterRound(ref state[3], ref state[7], ref state[11], ref state[15]);
				QuarterRound(ref state[0], ref state[5], ref state[10], ref state[15]);
				QuarterRound(ref state[1], ref state[6], ref state[11], ref state[12]);
				QuarterRound(ref state[2], ref state[7], ref state[8], ref state[13]);
				QuarterRound(ref state[3], ref state[4], ref state[9], ref state[14]);
			}
		}

		private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
		{
			a += b;
			d = BitUtils.RotateLeft(d ^ a, 16);
			c += d;
			b = BitUtils.RotateLeft(b ^ c, 12);
			a += b;
			d = BitUtils.RotateLeft(d ^ a, 8);
			c += d;
			b = BitUtils.RotateLeft(b ^ c, 7);
		}

		private static void SetSigma(System.Span<uint> state)
		{
			state[0] = SIGMA[0];
			state[1] = SIGMA[1];
			state[2] = SIGMA[2];
			state[3] = SIGMA[3];
		}

		private static void SetKey(System.Span<uint> state, System.ReadOnlySpan<byte> key)
		{
			state[4] = ArrayUtils.LoadUInt32LittleEndian(key, 0);
			state[5] = ArrayUtils.LoadUInt32LittleEndian(key, 4);
			state[6] = ArrayUtils.LoadUInt32LittleEndian(key, 8);
			state[7] = ArrayUtils.LoadUInt32LittleEndian(key, 12);
			state[8] = ArrayUtils.LoadUInt32LittleEndian(key, 16);
			state[9] = ArrayUtils.LoadUInt32LittleEndian(key, 20);
			state[10] = ArrayUtils.LoadUInt32LittleEndian(key, 24);
			state[11] = ArrayUtils.LoadUInt32LittleEndian(key, 28);
		}
	}

	private static class ArrayUtils
	{
		public static uint LoadUInt32LittleEndian(System.ReadOnlySpan<byte> buf, int offset)
		{
			return BinaryPrimitives.ReadUInt32LittleEndian(buf.Slice(offset, 4));
		}

		public static void StoreArray8UInt32LittleEndian(System.Span<byte> output, int offset, System.ReadOnlySpan<uint> input)
		{
			StoreArrayUInt32LittleEndian(output, offset, input, 8);
		}

			public static void StoreArrayUInt32LittleEndian(System.Span<byte> output, int offset, System.ReadOnlySpan<uint> input, int size)
			{
				int num = 4;
				int num2 = offset;
				for (int i = 0; i < size; i++)
				{
					BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(num2, num), input[i]);
					num2 += num;
				}
			}
	}

	private static class BitUtils
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint RotateLeft(uint value, int offset)
		{
			return (value << offset) | (value >> 32 - offset);
		}
	}

	public const string CryptoAlgorithmName = "xchacha20_poly1305";

	private const int NonceSizeBytes = 24;

	private const int KeySizeBytes = 32;

	private const int MacSizeBytes = 16;

	public string Name => "xchacha20_poly1305";

	public int ExpectedKeySize => 32;

	public int ExpectedNonceSize => 24;

	public byte[] Encrypt(byte[] message, byte[] key, byte[] nonce)
	{
		return EncryptOrDecrypt(forEncryption: true, message, key, nonce);
	}

	public byte[] Decrypt(byte[] cipher, byte[] key, byte[] nonce)
	{
		return EncryptOrDecrypt(forEncryption: false, cipher, key, nonce);
	}

	private byte[] EncryptOrDecrypt(bool forEncryption, byte[] data, byte[] key, byte[] nonce)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Expected O, but got Unknown
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		if (ExpectedKeySize != key.Length)
		{
			throw new CryptoException("Invalid key size");
		}
		if (ExpectedNonceSize != nonce.Length)
		{
			throw new CryptoException("Invalid nonce size");
		}
		byte[] array = new byte[32];
		ChaCha20Base.HChaCha20((array), (key), (nonce));
		byte[] array2 = CreateChaChaNonce(nonce);
		ICipherParameters val = (ICipherParameters)new AeadParameters(new KeyParameter(array), 128, array2, (byte[])null);
		IAeadCipher val2 = (IAeadCipher)new ChaCha20Poly1305();
		val2.Init(forEncryption, val);
		byte[] array3 = new byte[val2.GetOutputSize(data.Length)];
		int num = val2.ProcessBytes(data, 0, data.Length, array3, 0);
		val2.DoFinal(array3, num);
		return array3;
	}

	private static byte[] CreateChaChaNonce(byte[] nonce)
	{
		byte[] array = new byte[12];
		Array.Clear(array, 0, 4);
		Array.Copy(nonce, 16, array, 4, 8);
		return array;
	}
}


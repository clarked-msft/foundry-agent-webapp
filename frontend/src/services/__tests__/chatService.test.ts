import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ChatService } from '../chatService';
import type { AppAction } from '../../types/appState';
import type { Dispatch } from 'react';

// Mock the auth module
vi.mock('../../config/authConfig', () => ({
  msalConfig: { auth: { clientId: 'test', authority: 'https://login.microsoftonline.com/test' } },
  loginRequest: { scopes: ['api://test/Chat.ReadWrite'] },
  tokenRequest: { scopes: ['api://test/Chat.ReadWrite'], forceRefresh: false },
}));

describe('ChatService', () => {
  let chatService: ChatService;
  let mockDispatch: Dispatch<AppAction>;
  let mockGetAccessToken: () => Promise<string | null>;

  beforeEach(() => {
    vi.restoreAllMocks();
    mockDispatch = vi.fn() as Dispatch<AppAction>;
    mockGetAccessToken = vi.fn().mockResolvedValue('test-token');
    chatService = new ChatService('/api', mockGetAccessToken, mockDispatch);
  });

  describe('listConversations', () => {
    it('calls fetch with default limit of 20', async () => {
      const mockResponse = { conversations: [], hasMore: false };
      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockResponse),
      });
      vi.stubGlobal('fetch', fetchMock);

      await chatService.listConversations();

      expect(fetchMock).toHaveBeenCalledWith(
        '/api/conversations?limit=20',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Bearer test-token',
          }),
        }),
      );
    });

    it('calls fetch with custom limit', async () => {
      const mockResponse = { conversations: [], hasMore: false };
      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockResponse),
      });
      vi.stubGlobal('fetch', fetchMock);

      await chatService.listConversations(50);

      expect(fetchMock).toHaveBeenCalledWith(
        '/api/conversations?limit=50',
        expect.anything(),
      );
    });

    it('parses conversations and hasMore from response', async () => {
      const mockResponse = {
        conversations: [
          { id: 'c1', title: 'Conv 1', createdAt: 1 },
          { id: 'c2', title: 'Conv 2', createdAt: 2 },
        ],
        hasMore: true,
      };
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockResponse),
      }));

      const result = await chatService.listConversations();

      expect(result.conversations).toHaveLength(2);
      expect(result.hasMore).toBe(true);
    });

    it('throws on non-ok response', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
      }));

      await expect(chatService.listConversations()).rejects.toThrow();
    });
  });

  describe('deleteConversation', () => {
    it('calls DELETE to the correct URL', async () => {
      const fetchMock = vi.fn().mockResolvedValue({ ok: true });
      vi.stubGlobal('fetch', fetchMock);

      await chatService.deleteConversation('conv-123');

      expect(fetchMock).toHaveBeenCalledWith(
        '/api/conversations/conv-123',
        expect.objectContaining({
          method: 'DELETE',
          headers: expect.objectContaining({
            Authorization: 'Bearer test-token',
          }),
        }),
      );
    });

    it('throws on non-ok response', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
      }));

      await expect(chatService.deleteConversation('conv-123')).rejects.toThrow();
    });
  });

  describe('getConversationMessages', () => {
    it('calls GET to the correct URL', async () => {
      const mockMessages = [{ role: 'user', content: 'Hello' }];
      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockMessages),
      });
      vi.stubGlobal('fetch', fetchMock);

      const result = await chatService.getConversationMessages('conv-123');

      expect(fetchMock).toHaveBeenCalledWith(
        '/api/conversations/conv-123/messages',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Bearer test-token',
          }),
        }),
      );
      expect(result).toEqual(mockMessages);
    });

    it('throws on non-ok response', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
      }));

      await expect(chatService.getConversationMessages('conv-123')).rejects.toThrow();
    });
  });

  describe('sendMessage', () => {
    it('preserves consecutive identical chunks in streamed Markdown tables', async () => {
      const streamContent = [
        'data: {"type":"conversationId","conversationId":"conv-1"}\n\n',
        'data: {"type":"chunk","content":"|---"}\n\n',
        'data: {"type":"chunk","content":"|---"}\n\n',
        'data: {"type":"chunk","content":"|---"}\n\n',
        'data: {"type":"chunk","content":"|---|\\n"}\n\n',
        'data: {"type":"done"}\n\n',
      ].join('');
      const encodedContent = new TextEncoder().encode(streamContent);
      const reader = {
        read: vi.fn()
          .mockResolvedValueOnce({ done: false, value: encodedContent })
          .mockResolvedValueOnce({ done: true, value: undefined }),
        releaseLock: vi.fn(),
      };

      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
        ok: true,
        body: {
          getReader: () => reader,
        },
      }));

      await chatService.sendMessage('List incidents', null);

      const streamedChunks = vi.mocked(mockDispatch).mock.calls
        .map(([action]) => action)
        .filter((action): action is Extract<AppAction, { type: 'CHAT_STREAM_CHUNK' }> =>
          action.type === 'CHAT_STREAM_CHUNK')
        .map(action => action.content);

      expect(streamedChunks).toEqual(['|---', '|---', '|---', '|---|\n']);
    });
  });
});

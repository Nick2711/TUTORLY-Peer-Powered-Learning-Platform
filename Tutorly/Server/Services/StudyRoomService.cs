using Supabase;
using Supabase.Postgrest;
using Tutorly.Server.DatabaseModels;
using Tutorly.Server.Helpers;
using Tutorly.Shared;
using System.Text.Json;

namespace Tutorly.Server.Services;

public class StudyRoomService : IStudyRoomService
{
    private readonly ISupabaseClientFactory _supabaseFactory;
    private readonly ILogger<StudyRoomService> _logger;
    private readonly MeteredRoomService _meteredRoomService;
    private readonly IMessagingService _messagingService;

    public StudyRoomService(
        ISupabaseClientFactory supabaseFactory,
        ILogger<StudyRoomService> logger,
        MeteredRoomService meteredRoomService,
        IMessagingService messagingService)
    {
        _supabaseFactory = supabaseFactory;
        _logger = logger;
        _meteredRoomService = meteredRoomService;
        _messagingService = messagingService;
    }

    public async Task<CreateStudyRoomResponse> CreateRoomAsync(CreateStudyRoomRequest request, Guid currentUserId)
    {
        try
        {
            _logger.LogInformation($"Creating study room: {request.RoomName} by user {currentUserId}");

            var client = _supabaseFactory.CreateService();

            // Generate room code for private rooms
            string? roomCode = null;
            if (request.Privacy == "PrivateInviteOnly")
            {
                roomCode = GenerateRoomCode();
            }

            var room = new StudyRoomEntity
            {
                RoomId = Guid.NewGuid(),
                RoomName = request.RoomName,
                Description = request.Description,
                CreatorUserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
                ScheduledStartTime = request.ScheduledStartTime,
                ScheduledEndTime = request.ScheduledEndTime,
                RoomType = request.RoomType,
                Privacy = request.Privacy,
                ModuleId = request.ModuleId,
                MaxParticipants = request.MaxParticipants,
                Status = request.ScheduledStartTime.HasValue && request.ScheduledStartTime > DateTime.UtcNow
                    ? "Scheduled"
                    : "Active",
                RoomCode = roomCode,
                UpdatedAt = DateTime.UtcNow
            };

            var response = await client.From<StudyRoomEntity>().Insert(room);
            var createdRoom = response.Models.FirstOrDefault();

            if (createdRoom == null)
            {
                return new CreateStudyRoomResponse
                {
                    Success = false,
                    Message = "Failed to create room"
                };
            }

            var roomDto = await MapToRoomDto(createdRoom, client);

            // Create Metered room
            var meteredRoomUrl = await _meteredRoomService.CreateRoomAsync(createdRoom.RoomId, request.RoomName, "private");
            if (!string.IsNullOrEmpty(meteredRoomUrl))
            {
                // Update room with Metered URL
                await client
                    .From<StudyRoomEntity>()
                    .Where(r => r.RoomId == createdRoom.RoomId)
                    .Set(r => r.RoomCode, meteredRoomUrl)
                    .Update();

                roomDto.RoomCode = meteredRoomUrl;
            }

            _logger.LogInformation($"Study room created successfully: {createdRoom.RoomId}");

            return new CreateStudyRoomResponse
            {
                Success = true,
                Message = "Room created successfully",
                Room = roomDto
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating study room: {request.RoomName}");
            return new CreateStudyRoomResponse
            {
                Success = false,
                Message = $"Error creating room: {ex.Message}"
            };
        }
    }

    public async Task<StudyRoomDto?> GetRoomByIdAsync(Guid roomId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            var response = await client
                .From<StudyRoomEntity>()
                .Where(r => r.RoomId == roomId)
                .Get();

            var room = response.Models.FirstOrDefault();
            if (room == null) return null;

            return await MapToRoomDto(room, client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting room: {roomId}");
            return null;
        }
    }

    public async Task<List<StudyRoomDto>> GetAvailableRoomsAsync(StudyRoomFilters filters, Guid currentUserId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            // Build a list of all entities, then filter in-memory for flexibility
            // (Alternative: use Filter() method with column names instead of Where() lambdas)
            var allRoomsResponse = await client
                .From<StudyRoomEntity>()
                .Get();

            var allRooms = allRoomsResponse.Models.AsQueryable();

            // Apply status filter
            if (!string.IsNullOrEmpty(filters.Status))
            {
                allRooms = allRooms.Where(r => r.Status == filters.Status);
            }
            else
            {
                // Default: show Active and Scheduled rooms only
                allRooms = allRooms.Where(r => r.Status == "Active" || r.Status == "Scheduled");
            }

            // Apply additional filters
            if (!string.IsNullOrEmpty(filters.RoomType))
            {
                allRooms = allRooms.Where(r => r.RoomType == filters.RoomType);
            }

            if (!string.IsNullOrEmpty(filters.Privacy))
            {
                allRooms = allRooms.Where(r => r.Privacy == filters.Privacy);
            }
            else
            {
                // Default: show Public and ModuleSpecific (not private)
                allRooms = allRooms.Where(r => r.Privacy == "Public" || r.Privacy == "ModuleSpecific");
            }

            if (filters.ModuleId.HasValue)
            {
                allRooms = allRooms.Where(r => r.ModuleId == filters.ModuleId.Value);
            }

            // Order and convert to list
            var filteredRooms = allRooms.OrderByDescending(r => r.CreatedAt).ToList();

            var rooms = new List<StudyRoomDto>();
            foreach (var room in filteredRooms)
            {
                var roomDto = await MapToRoomDto(room, client);
                rooms.Add(roomDto);
            }

            return rooms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available rooms");
            return new List<StudyRoomDto>();
        }
    }

    public async Task<List<StudyRoomDto>> GetMySessionsAsync(Guid currentUserId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            // Get rooms created by user or rooms they're participating in
            var createdRooms = await client
                .From<StudyRoomEntity>()
                .Where(r => r.CreatorUserId == currentUserId)
                .Where(r => r.Status == "Active" || r.Status == "Scheduled")
                .Order(r => r.ScheduledStartTime, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var rooms = new List<StudyRoomDto>();
            foreach (var room in createdRooms.Models)
            {
                var roomDto = await MapToRoomDto(room, client);
                rooms.Add(roomDto);
            }

            // Also get rooms where user is a participant
            var participantResponse = await client
                .From<StudyRoomParticipantEntity>()
                .Where(p => p.UserId == currentUserId)
                .Where(p => p.IsActive == true)
                .Get();

            var participantRoomIds = participantResponse.Models.Select(p => p.RoomId).ToList();

            if (participantRoomIds.Any())
            {
                foreach (var roomId in participantRoomIds)
                {
                    var participantRooms = await client
                        .From<StudyRoomEntity>()
                        .Where(r => r.RoomId == roomId)
                        .Where(r => r.Status == "Active" || r.Status == "Scheduled")
                        .Get();

                    foreach (var room in participantRooms.Models)
                    {
                        if (!rooms.Any(r => r.RoomId == room.RoomId))
                        {
                            var roomDto = await MapToRoomDto(room, client);
                            rooms.Add(roomDto);
                        }
                    }
                }
            }

            return rooms.OrderBy(r => r.ScheduledStartTime ?? r.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting sessions for user: {currentUserId}");
            return new List<StudyRoomDto>();
        }
    }

    public async Task<JoinRoomResponse> JoinRoomAsync(JoinRoomRequest request, Guid currentUserId, string connectionId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            var room = await client
                .From<StudyRoomEntity>()
                .Where(r => r.RoomId == request.RoomId)
                .Get();

            var roomEntity = room.Models.FirstOrDefault();
            if (roomEntity == null)
            {
                return new JoinRoomResponse
                {
                    Success = false,
                    Message = "Room not found"
                };
            }

            // Check if room is active or scheduled
            if (roomEntity.Status == "Ended")
            {
                return new JoinRoomResponse
                {
                    Success = false,
                    Message = "Room has ended"
                };
            }

            // Check privacy settings
            if (roomEntity.Privacy == "PrivateInviteOnly")
            {
                if (roomEntity.RoomType != "Standalone")
                {
                    // Non-Standalone private rooms require a code
                    if (string.IsNullOrEmpty(request.RoomCode) || request.RoomCode != roomEntity.RoomCode)
                    {
                        return new JoinRoomResponse
                        {
                            Success = false,
                            Message = "Invalid room code"
                        };
                    }
                }
                else
                {
                    // Standalone private rooms: restrict to session participants (tutor/student) or creator
                    var sessionResp = await client
                        .From<SessionEntity>()
                        .Where(s => s.StudyRoomId == roomEntity.RoomId)
                        .Get();

                    var linkedSession = sessionResp.Models.FirstOrDefault();
                    if (linkedSession != null)
                    {
                        var allowedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        var studentResp = await client
                            .From<StudentProfileEntity>()
                            .Where(s => s.StudentId == linkedSession.StudentId)
                            .Get();
                        var studentUserId = studentResp.Models.FirstOrDefault()?.UserId;
                        if (!string.IsNullOrEmpty(studentUserId)) allowedUserIds.Add(studentUserId);

                        var tutorResp = await client
                            .From<TutorProfileEntity>()
                            .Where(t => t.TutorId == linkedSession.TutorId)
                            .Get();
                        var tutorUserId = tutorResp.Models.FirstOrDefault()?.UserId;
                        if (!string.IsNullOrEmpty(tutorUserId)) allowedUserIds.Add(tutorUserId);

                        allowedUserIds.Add(roomEntity.CreatorUserId.ToString());

                        if (!allowedUserIds.Contains(currentUserId.ToString()))
                        {
                            return new JoinRoomResponse
                            {
                                Success = false,
                                Message = "You are not a participant of this session"
                            };
                        }
                    }
                }
            }

            // Check participant count
            var existingParticipants = await GetRoomParticipantsAsync(request.RoomId);
            if (existingParticipants.Count >= roomEntity.MaxParticipants)
            {
                return new JoinRoomResponse
                {
                    Success = false,
                    Message = "Room is full"
                };
            }

            // Get user info from profiles
            var userName = "User";
            var userEmail = "";
            var userRole = "Student";
            var userIdString = currentUserId.ToString();

            try
            {
                // Try to get student profile first
                var studentProfile = await client
                    .From<StudentProfileEntity>()
                    .Where(s => s.UserId == userIdString)
                    .Get();

                if (studentProfile.Models.Any())
                {
                    var student = studentProfile.Models.FirstOrDefault();
                    userName = student?.FullName ?? "User";
                    userEmail = ""; // StudentProfileEntity doesn't have Email field
                    userRole = "Student";
                }
                else
                {
                    // Try tutor profile
                    var tutorProfile = await client
                        .From<TutorProfileEntity>()
                        .Where(t => t.UserId == userIdString)
                        .Get();

                    if (tutorProfile.Models.Any())
                    {
                        var tutor = tutorProfile.Models.FirstOrDefault();
                        userName = tutor?.FullName ?? "User";
                        userEmail = ""; // TutorProfileEntity doesn't have Email field
                        userRole = "Tutor";
                    }
                    else
                    {
                        // Try admin profile
                        var adminProfile = await client
                            .From<AdminProfileEntity>()
                            .Where(a => a.UserId == userIdString)
                            .Get();

                        if (adminProfile.Models.Any())
                        {
                            var admin = adminProfile.Models.FirstOrDefault();
                            userName = admin?.FullName ?? "Admin";
                            userEmail = ""; // AdminProfileEntity doesn't have Email field
                            userRole = "Admin";
                        }
                    }
                }

                // Fallback if name is empty
                if (string.IsNullOrWhiteSpace(userName))
                {
                    userName = "User";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not fetch user profile for {currentUserId}, using default name");
            }

            // Check if participant already exists (for rejoin scenarios)
            var existingParticipant = await client.From<StudyRoomParticipantEntity>()
                .Where(p => p.RoomId == request.RoomId && p.UserId == currentUserId)
                .Single();

            if (existingParticipant != null)
            {
                // Update existing participant
                existingParticipant.ConnectionId = connectionId;
                existingParticipant.JoinedAt = DateTime.UtcNow;
                existingParticipant.IsActive = true;
                existingParticipant.LastSeen = DateTime.UtcNow;

                await client.From<StudyRoomParticipantEntity>()
                    .Where(p => p.Id == existingParticipant.Id)
                    .Update(existingParticipant);
            }
            else
            {
                // Create new participant
                var participant = new StudyRoomParticipantEntity
                {
                    Id = Guid.NewGuid(),
                    RoomId = request.RoomId,
                    UserId = currentUserId,
                    UserName = userName,
                    UserEmail = userEmail,
                    UserRole = userRole,
                    ConnectionId = connectionId,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true,
                    LastSeen = DateTime.UtcNow
                };

                await client.From<StudyRoomParticipantEntity>().Insert(participant);
            }

            // Update room status to Active if it was Scheduled
            if (roomEntity.Status == "Scheduled")
            {
                await UpdateRoomStatusAsync(request.RoomId, "Active");
            }

            var roomDto = await MapToRoomDto(roomEntity, client);
            var participants = await GetRoomParticipantsAsync(request.RoomId);

            _logger.LogInformation($"User {currentUserId} joined room {request.RoomId}");

            return new JoinRoomResponse
            {
                Success = true,
                Message = "Joined room successfully",
                Room = roomDto,
                CurrentParticipants = participants
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error joining room: {request.RoomId}");
            return new JoinRoomResponse
            {
                Success = false,
                Message = $"Error joining room: {ex.Message}"
            };
        }
    }

    public async Task<bool> LeaveRoomAsync(Guid roomId, Guid userId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();


            var participants = await client
                .From<StudyRoomParticipantEntity>()
                .Where(p => p.RoomId == roomId)
                .Where(p => p.UserId == userId)
                .Where(p => p.IsActive == true)
                .Get();

            foreach (var participant in participants.Models)
            {
                participant.IsActive = false;
                participant.LeftAt = DateTime.UtcNow;
                await client.From<StudyRoomParticipantEntity>()
                    .Update(participant);
            }

            _logger.LogInformation($"User {userId} left room {roomId}");

            return true;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("No such host is known") || ex.Message.Contains("connection was forcibly closed"))
        {
            _logger.LogWarning("Database connection failed. WebRTC will continue without room leave persistence: {Message}", ex.Message);
            return true; // Return true to allow WebRTC to continue working
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error leaving room: {roomId}");
            return false;
        }
    }

    public async Task<bool> UpdateRoomStatusAsync(Guid roomId, string status)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            var room = await client
                .From<StudyRoomEntity>()
                .Where(r => r.RoomId == roomId)
                .Get();

            var roomEntity = room.Models.FirstOrDefault();
            if (roomEntity == null) return false;

            roomEntity.Status = status;
            roomEntity.UpdatedAt = DateTime.UtcNow;

            await client.From<StudyRoomEntity>().Update(roomEntity);

            _logger.LogInformation($"Room {roomId} status updated to {status}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating room status: {roomId}");
            return false;
        }
    }

    public async Task<bool> UpdateParticipantStatusAsync(Guid roomId, Guid userId, bool? isMuted, bool? isScreenSharing, bool? isVideoEnabled = null)
    {
        try
        {
            var client = _supabaseFactory.CreateService();


            var participants = await client
                .From<StudyRoomParticipantEntity>()
                .Where(p => p.RoomId == roomId)
                .Where(p => p.UserId == userId)
                .Where(p => p.IsActive == true)
                .Get();

            var participant = participants.Models.FirstOrDefault();
            if (participant == null) return false;

            if (isMuted.HasValue)
                participant.IsMuted = isMuted.Value;

            if (isScreenSharing.HasValue)
                participant.IsScreenSharing = isScreenSharing.Value;

            if (isVideoEnabled.HasValue)
                participant.IsVideoEnabled = isVideoEnabled.Value;

            // Update last seen timestamp
            participant.LastSeen = DateTime.UtcNow;

            await client.From<StudyRoomParticipantEntity>().Update(participant);

            return true;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("No such host is known") || ex.Message.Contains("connection was forcibly closed"))
        {
            _logger.LogWarning("Database connection failed. WebRTC will continue without participant status persistence: {Message}", ex.Message);
            return true; // Return true to allow WebRTC to continue working
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating participant status");
            return false;
        }
    }

    public async Task<List<StudyRoomParticipantDto>> GetRoomParticipantsAsync(Guid roomId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            var response = await client
                .From<StudyRoomParticipantEntity>()
                .Where(p => p.RoomId == roomId)
                .Where(p => p.IsActive == true)
                .Get();

            return response.Models.Select(p => new StudyRoomParticipantDto
            {
                UserId = p.UserId,
                UserName = p.UserName,
                UserEmail = p.UserEmail,
                UserRole = p.UserRole,
                ConnectionId = p.ConnectionId,
                JoinedAt = p.JoinedAt,
                IsActive = p.IsActive,
                IsMuted = p.IsMuted,
                IsScreenSharing = p.IsScreenSharing,
                IsVideoEnabled = p.IsVideoEnabled,
                LastSeen = p.LastSeen,
                AvatarUrl = $"https://i.pravatar.cc/320?u={p.UserId}"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting room participants: {roomId}");
            return new List<StudyRoomParticipantDto>();
        }
    }

    public async Task<bool> DeleteRoomAsync(Guid roomId, Guid currentUserId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            var room = await client
                .From<StudyRoomEntity>()
                .Where(r => r.RoomId == roomId)
                .Get();

            var roomEntity = room.Models.FirstOrDefault();
            if (roomEntity == null) return false;

            // Only creator can delete
            if (roomEntity.CreatorUserId != currentUserId)
            {
                _logger.LogWarning($"User {currentUserId} attempted to delete room {roomId} they don't own");
                return false;
            }

            // Mark as ended instead of deleting
            roomEntity.Status = "Ended";
            roomEntity.UpdatedAt = DateTime.UtcNow;

            await client.From<StudyRoomEntity>().Update(roomEntity);

            // Deactivate all participants
            var participants = await client
                .From<StudyRoomParticipantEntity>()
                .Where(p => p.RoomId == roomId)
                .Where(p => p.IsActive == true)
                .Get();

            foreach (var participant in participants.Models)
            {
                participant.IsActive = false;
                participant.LeftAt = DateTime.UtcNow;
                await client.From<StudyRoomParticipantEntity>().Update(participant);
            }

            _logger.LogInformation($"Room {roomId} ended by creator {currentUserId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting room: {roomId}");
            return false;
        }
    }

    /// <summary>
    /// Delete room without user validation (for call cancellation)
    /// </summary>
    public async Task<bool> DeleteRoomAsync(Guid roomId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            var room = await client
                .From<StudyRoomEntity>()
                .Where(r => r.RoomId == roomId)
                .Get();

            var roomEntity = room.Models.FirstOrDefault();
            if (roomEntity == null) return false;

            // Mark as ended instead of deleting
            roomEntity.Status = "Ended";
            roomEntity.UpdatedAt = DateTime.UtcNow;

            await client.From<StudyRoomEntity>().Update(roomEntity);

            // Deactivate all participants
            var participants = await client
                .From<StudyRoomParticipantEntity>()
                .Where(p => p.RoomId == roomId)
                .Where(p => p.IsActive == true)
                .Get();

            foreach (var participant in participants.Models)
            {
                participant.IsActive = false;
                participant.LeftAt = DateTime.UtcNow;
                await client.From<StudyRoomParticipantEntity>().Update(participant);
            }

            _logger.LogInformation($"Room {roomId} deleted (call cancellation)");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting room: {roomId}");
            return false;
        }
    }

    public async Task<bool> CanJoinRoomAsync(Guid roomId, Guid userId, string? roomCode)
    {
        try
        {
            var room = await GetRoomByIdAsync(roomId);
            if (room == null) return false;

            if (room.Status == "Ended") return false;

            if (room.Privacy == "PrivateInviteOnly")
            {
                if (room.RoomType != "Standalone")
                {
                    if (string.IsNullOrEmpty(roomCode) || roomCode != room.RoomCode)
                    {
                        return false;
                    }
                }
                else
                {
                    // Standalone private rooms: allow only session participants or creator
                    var client = _supabaseFactory.CreateService();
                    var sessionResp = await client
                        .From<SessionEntity>()
                        .Where(s => s.StudyRoomId == roomId)
                        .Get();
                    var linkedSession = sessionResp.Models.FirstOrDefault();
                    if (linkedSession != null)
                    {
                        var allowedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            room.CreatorUserId.ToString()
                        };

                        var studentResp = await client
                            .From<StudentProfileEntity>()
                            .Where(s => s.StudentId == linkedSession.StudentId)
                            .Get();
                        var studentUserId = studentResp.Models.FirstOrDefault()?.UserId;
                        if (!string.IsNullOrEmpty(studentUserId)) allowedUserIds.Add(studentUserId);

                        var tutorResp = await client
                            .From<TutorProfileEntity>()
                            .Where(t => t.TutorId == linkedSession.TutorId)
                            .Get();
                        var tutorUserId = tutorResp.Models.FirstOrDefault()?.UserId;
                        if (!string.IsNullOrEmpty(tutorUserId)) allowedUserIds.Add(tutorUserId);

                        if (!allowedUserIds.Contains(userId.ToString()))
                        {
                            return false;
                        }
                    }
                }
            }

            if (room.CurrentParticipantCount >= room.MaxParticipants)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking room access: {roomId}");
            return false;
        }
    }

    #region Helper Methods

    private async Task<StudyRoomDto> MapToRoomDto(StudyRoomEntity room, Supabase.Client client)
    {
        var participants = await GetRoomParticipantsAsync(room.RoomId);

        return new StudyRoomDto
        {
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            Description = room.Description,
            CreatorUserId = room.CreatorUserId,
            CreatedAt = room.CreatedAt,
            ScheduledStartTime = room.ScheduledStartTime,
            ScheduledEndTime = room.ScheduledEndTime,
            RoomType = room.RoomType,
            Privacy = room.Privacy,
            ModuleId = room.ModuleId,
            MaxParticipants = room.MaxParticipants,
            Status = room.Status,
            RoomCode = room.Privacy == "PrivateInviteOnly" ? room.RoomCode : null, // Only show code for private rooms
            CurrentParticipantCount = participants.Count,
            Participants = participants
        };
    }

    public async Task<bool> CleanupEmptyRoomAsync(Guid roomId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();


            // Check if room has any active participants
            var participants = await client
                .From<StudyRoomParticipantEntity>()
                .Where(p => p.RoomId == roomId)
                .Where(p => p.IsActive == true)
                .Get();

            // If no active participants, delete the room
            if (!participants.Models.Any())
            {
                await client
                    .From<StudyRoomEntity>()
                    .Where(r => r.RoomId == roomId)
                    .Delete();

                _logger.LogInformation($"Room {roomId} cleaned up - no active participants");
                return true;
            }

            return false; // Room still has participants
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("No such host is known") || ex.Message.Contains("connection was forcibly closed"))
        {
            _logger.LogWarning("Database connection failed. Skipping room cleanup: {Message}", ex.Message);
            return true; // Return true to allow WebRTC to continue working
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cleaning up room {roomId}");
            return false;
        }
    }

    public async Task CleanupAllEmptyRoomsAsync()
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            // Get all rooms
            var rooms = await client
                .From<StudyRoomEntity>()
                .Get();

            foreach (var room in rooms.Models)
            {
                // Check if room has any active participants
                var participants = await client
                    .From<StudyRoomParticipantEntity>()
                    .Where(p => p.RoomId == room.RoomId)
                    .Where(p => p.IsActive == true)
                    .Get();

                // If no active participants, delete the room
                if (!participants.Models.Any())
                {
                    await client
                        .From<StudyRoomEntity>()
                        .Where(r => r.RoomId == room.RoomId)
                        .Delete();

                    _logger.LogInformation($"Cleaned up empty room: {room.RoomName} ({room.RoomId})");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up empty rooms");
        }
    }

    /// <summary>
    /// Clean up rooms with single inactive users (10-minute timeout)
    /// </summary>
    public async Task CleanupInactiveSingleUserRoomsAsync()
    {
        try
        {
            var client = _supabaseFactory.CreateService();
            var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10);

            // Get all active rooms
            var rooms = await client
                .From<StudyRoomEntity>()
                .Where(r => r.Status == "Active")
                .Get();

            foreach (var room in rooms.Models)
            {
                // Get active participants for this room
                var participants = await client
                    .From<StudyRoomParticipantEntity>()
                    .Where(p => p.RoomId == room.RoomId)
                    .Where(p => p.IsActive == true)
                    .Get();

                // If only one participant and they've been inactive for 10+ minutes
                if (participants.Models.Count == 1)
                {
                    var participant = participants.Models.First();
                    if (participant.LastSeen < tenMinutesAgo)
                    {
                        _logger.LogInformation($"Room {room.RoomId} has single inactive user for 10+ minutes, cleaning up");

                        // Mark participant as inactive
                        participant.IsActive = false;
                        participant.LeftAt = DateTime.UtcNow;
                        await client.From<StudyRoomParticipantEntity>().Update(participant);

                        // Delete the room
                        await client
                            .From<StudyRoomEntity>()
                            .Where(r => r.RoomId == room.RoomId)
                            .Delete();

                        _logger.LogInformation($"Room {room.RoomId} cleaned up due to inactivity");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up inactive single user rooms");
        }
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    #endregion

    #region Chat Methods

    public async Task<RoomChatMessageDto> SaveChatMessageAsync(Guid roomId, Guid userId, string message)
    {
        try
        {
            _logger.LogInformation($"💬 SaveChatMessageAsync: roomId={roomId}, userId={userId}, message='{message}'");

            // Get room to access room name
            var client = _supabaseFactory.CreateService();
            var room = await client.From<StudyRoomEntity>()
                .Where(r => r.RoomId == roomId)
                .Single();

            if (room == null)
            {
                _logger.LogError($"💬 SaveChatMessageAsync: Room {roomId} not found");
                throw new InvalidOperationException($"Room {roomId} not found");
            }

            _logger.LogInformation($"💬 SaveChatMessageAsync: Found room '{room.RoomName}', creating/getting conversation");

            // Lazy initialization: Create/get conversation only when first message is sent
            var conversationId = await CreateOrGetRoomConversationAsync(roomId, room.RoomName);

            _logger.LogInformation($"💬 SaveChatMessageAsync: Got conversation ID {conversationId}, sending message via messaging service");

            // Use existing messaging service to send message
            var sendMessageDto = new SendMessageDto
            {
                Content = message,
                MessageType = MessageType.Text
            };

            var result = await _messagingService.SendMessageAsync(conversationId, sendMessageDto, userId.ToString());

            if (!result.Success)
            {
                _logger.LogError($"💬 SaveChatMessageAsync: Failed to send message via messaging service: {result.Message}");
                throw new InvalidOperationException($"Failed to send message: {result.Message}");
            }

            _logger.LogInformation($"💬 SaveChatMessageAsync: Message sent successfully, creating DTO");

            // Convert MessageDto to RoomChatMessageDto for compatibility
            return new RoomChatMessageDto
            {
                MessageId = Guid.NewGuid(), // Generate new Guid for study room context
                RoomId = roomId,
                UserId = userId,
                UserName = result.Data.SenderName ?? "User",
                Message = result.Data.Content,
                Timestamp = result.Data.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error saving chat message for room {roomId}");
            throw;
        }
    }

    public async Task<List<RoomChatMessageDto>> GetRoomChatHistoryAsync(Guid roomId)
    {
        try
        {
            var client = _supabaseFactory.CreateService();
            var room = await client.From<StudyRoomEntity>()
                .Where(r => r.RoomId == roomId)
                .Single();

            if (room == null)
            {
                return new List<RoomChatMessageDto>();
            }

            // Check if conversation exists for this room
            var groupName = $"{room.RoomName} - Chat";
            var existing = await client.From<ConversationEntity>()
                .Where(x => x.GroupName == groupName)
                .Get();

            var conversation = existing.Models?.FirstOrDefault();

            if (conversation == null)
            {
                return new List<RoomChatMessageDto>(); // No conversation yet
            }

            // Use existing messaging service to get messages
            var filter = new MessageFilterDto { Limit = 100 };
            var result = await _messagingService.GetMessagesAsync(conversation.ConversationId, filter, "");

            if (!result.Success)
            {
                return new List<RoomChatMessageDto>();
            }

            // Convert MessageDto list to RoomChatMessageDto list
            return result.Data.Select(m => new RoomChatMessageDto
            {
                MessageId = Guid.NewGuid(), // Generate new Guid for study room context
                RoomId = roomId,
                UserId = Guid.Parse(m.SenderId),
                UserName = m.SenderName ?? "User",
                Message = m.Content,
                Timestamp = m.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting chat history for room {roomId}");
            return new List<RoomChatMessageDto>();
        }
    }

    public async Task<int> CreateOrGetRoomConversationAsync(Guid roomId, string roomName)
    {
        try
        {
            var client = _supabaseFactory.CreateService();

            // Get current room participants
            var currentParticipants = await GetRoomParticipantsAsync(roomId);
            var currentParticipantIds = currentParticipants
                .Select(p => p.UserId.ToString())
                .OrderBy(id => id)
                .ToList();

            // Smart matching: Find existing conversation with SAME name AND SAME participants
            var groupName = $"{roomName} - Chat";
            var existingConversations = await client.From<ConversationEntity>()
                .Where(x => x.GroupName == groupName)
                .Get();

            foreach (var conv in existingConversations.Models)
            {
                // Get participants for this conversation
                var convParticipants = await client.From<ConversationParticipantEntity>()
                    .Where(p => p.ConversationId == conv.ConversationId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null)
                    .Get();

                var convParticipantIds = convParticipants.Models
                    .Select(p => p.UserId)
                    .OrderBy(id => id)
                    .ToList();

                // If participants match exactly, reuse this conversation
                if (currentParticipantIds.SequenceEqual(convParticipantIds))
                {
                    _logger.LogInformation($"Found matching conversation for room '{roomName}' with same participants: {string.Join(", ", currentParticipantIds)}");
                    return conv.ConversationId;
                }
            }

            // No match found - Create NEW group conversation
            _logger.LogInformation($"No matching conversation found for room '{roomName}'. Creating new conversation with participants: {string.Join(", ", currentParticipantIds)}");

            var createDto = new CreateConversationDto
            {
                Type = ConversationType.Group,
                GroupName = $"{roomName} - Chat",
                GroupDescription = $"Study room chat for {roomName}",
                ParticipantUserIds = currentParticipantIds
            };

            var result = await _messagingService.CreateGroupConversationAsync(
                createDto,
                currentParticipantIds.FirstOrDefault() ?? ""
            );

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to create conversation: {result.Message}");
            }

            _logger.LogInformation($"Created new group conversation '{roomName} - Chat' for room {roomId}");
            return result.Data.ConversationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating/getting conversation for room {roomId}");
            throw;
        }
    }

    #endregion
}


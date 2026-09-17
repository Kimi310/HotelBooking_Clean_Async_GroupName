using System;
using System.Collections.Generic;
using HotelBooking.Core;
using HotelBooking.UnitTests.Fakes;
using Xunit;
using System.Linq;
using Moq;
using System.Threading.Tasks;


namespace HotelBooking.UnitTests
{
    public class BookingManagerTests
    {
        
        private readonly DateTime fullyOccupiedStart;
        private readonly DateTime fullyOccupiedEnd;
        private readonly Mock<IRepository<Booking>> mockBookingRepository;
        private readonly IBookingManager bookingManager;

        public BookingManagerTests()
        {
            DateTime start = DateTime.Today.AddDays(10);
            DateTime end = DateTime.Today.AddDays(20);

            mockBookingRepository = MockBookingRepositoryFactory.Create(start, end);
            IRepository<Room> roomRepository = new FakeRoomRepository();

            bookingManager = new BookingManager(mockBookingRepository.Object, roomRepository);
        }

        [Fact]
        public async Task FindAvailableRoom_StartDateNotInTheFuture_ThrowsArgumentException()
        {
            // Arrange
            DateTime date = DateTime.Today;

            // Act
            Task result() => bookingManager.FindAvailableRoom(date, date);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(result);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomAvailable_RoomIdNotMinusOne()
        {
            // Arrange
            DateTime date = DateTime.Today.AddDays(1);
            // Act
            int roomId = await bookingManager.FindAvailableRoom(date, date);
            // Assert
            Assert.NotEqual(-1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomAvailable_ReturnsAvailableRoom()
        {
            // This test was added to satisfy the following test design
            // principle: "Tests should have strong assertions".

            // Arrange
            DateTime date = DateTime.Today.AddDays(1);
            
            // Act
            int roomId = await bookingManager.FindAvailableRoom(date, date);

            var bookingForReturnedRoomId = (await mockBookingRepository.Object.GetAllAsync()).
                Where(b => b.RoomId == roomId
                           && b.StartDate <= date
                           && b.EndDate >= date
                           && b.IsActive);
            
            // Assert
            Assert.Empty(bookingForReturnedRoomId);
        }
        
        private async Task<Booking> ActAndCapture(Booking input)
        {
            Booking captured = null;
            mockBookingRepository
                .Setup(r => r.AddAsync(It.IsAny<Booking>()))
                .Callback<Booking>(b => captured = b)
                .Returns(Task.CompletedTask);

            await bookingManager.CreateBooking(input);
            return captured;
        }
        
        [Fact]
        public async Task CreateBooking_RoomAvailable_ReturnsTrue()
        {
            // Arrange
            mockBookingRepository.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Booking>());

            Booking booking = new Booking
            {
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(1)
            };

            // Act
            bool isCreated = await bookingManager.CreateBooking(booking);

            // Assert
            Assert.True(isCreated);
            mockBookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>()), Times.Once);
        }
        
        [Fact]
        public async Task CreateBooking_RoomAvailable_SetsIsActiveTrue()
        {
            var booking = new Booking { StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(2) };
            var saved = await ActAndCapture(booking);
            Assert.True(saved.IsActive);
        }

        [Fact]
        public async Task CreateBooking_RoomAvailable_PreservesCustomerId()
        {
            var booking = new Booking { StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(2), CustomerId = 99 };
            var saved = await ActAndCapture(booking);
            Assert.Equal(99, saved.CustomerId);
        }

        [Fact]
        public async Task CreateBooking_RoomAvailable_AssignsAvailableRoomId()
        {
            var booking = new Booking { StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(2) };
            var saved = await ActAndCapture(booking);
            Assert.True(saved.RoomId > 0);
        }

        [Fact]
        public async Task CreateBooking_RoomAvailable_PreservesDateRange()
        {
            var start = DateTime.Today.AddDays(1);
            var end = DateTime.Today.AddDays(3);
            var booking = new Booking { StartDate = start, EndDate = end };
            var saved = await ActAndCapture(booking);
            Assert.Equal(start, saved.StartDate);
            Assert.Equal(end, saved.EndDate);
        }

    }
}

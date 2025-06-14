USE DKMovies;
GO

-- COUNTRIES
CREATE TABLE Countries (
    ID INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) UNIQUE NOT NULL,
    Description NVARCHAR(255)
);

-- GENRES
CREATE TABLE Genres (
    ID INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) UNIQUE NOT NULL,
    Description NVARCHAR(255)
);

-- RATINGS
CREATE TABLE Ratings (
    ID INT IDENTITY PRIMARY KEY,
    Value NVARCHAR(10) UNIQUE NOT NULL,
    Description NVARCHAR(255)
);

-- LANGUAGES
CREATE TABLE Languages (
    ID INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) UNIQUE NOT NULL,
    Description NVARCHAR(MAX)
);

-- DIRECTORS
CREATE TABLE Directors (
    ID INT IDENTITY PRIMARY KEY,
    FullName NVARCHAR(255) UNIQUE NOT NULL,
    DateOfBirth DATE,
    Biography NVARCHAR(MAX),
    PlaceOfBirth NVARCHAR(255),
	ProfileImagePath NVARCHAR(500),
);

-- ACTORS
CREATE TABLE Actors (
    ID INT IDENTITY PRIMARY KEY,
    FullName NVARCHAR(255) UNIQUE NOT NULL,
    Biography NVARCHAR(MAX),
    DateOfBirth DATE,
    PlaceOfBirth NVARCHAR(255),
    ProfileImagePath NVARCHAR(255)
);

-- USERS
CREATE TABLE Users (
    ID INT IDENTITY PRIMARY KEY,
    Username NVARCHAR(255) UNIQUE NOT NULL,
    Email NVARCHAR(255) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(255),
    Phone NVARCHAR(20),
    BirthDate DATE,
    Gender NVARCHAR(10) CHECK (Gender IN ('Male', 'Female', 'Other')),
    ProfileImagePath NVARCHAR(500),
    CreatedAt DATETIME DEFAULT GETDATE(),
    EmailConfirmed BIT DEFAULT 0,
    ConfirmationCode NVARCHAR(100),
    TwoFactorEnabled BIT DEFAULT 0,
    TwoFactorCode NVARCHAR(100),
    TwoFactorExpiry DATETIME
);


-- THEATERS
CREATE TABLE Theaters (
    ID INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(255) UNIQUE NOT NULL,
    Location NVARCHAR(255) UNIQUE NOT NULL,
    Phone NVARCHAR(20)
);

-- THEATERIMAGES
CREATE TABLE TheaterImages (
    ID INT IDENTITY PRIMARY KEY,
    TheaterID INT NOT NULL,
    ImageUrl NVARCHAR(500) NOT NULL,
    UploadedAt DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_TheaterImages_Theaters FOREIGN KEY (TheaterID)
        REFERENCES Theaters(ID) ON DELETE CASCADE
);


-- AUDITORIUMS
CREATE TABLE Auditoriums (
    ID INT IDENTITY PRIMARY KEY,
    TheaterID INT FOREIGN KEY REFERENCES Theaters(ID) ON DELETE CASCADE,
    Name NVARCHAR(50) NOT NULL,
    Capacity INT NOT NULL
);

-- SEATS
CREATE TABLE Seats (
    ID INT IDENTITY PRIMARY KEY,
    AuditoriumID INT FOREIGN KEY REFERENCES Auditoriums(ID) ON DELETE CASCADE,
    RowLabel CHAR(1) NOT NULL,
    SeatNumber INT NOT NULL,
    CONSTRAINT UC_Seat UNIQUE (AuditoriumID, RowLabel, SeatNumber)
);

-- MOVIES
CREATE TABLE Movies (
    ID INT IDENTITY PRIMARY KEY,
    Title NVARCHAR(255) UNIQUE NOT NULL,
    Description NVARCHAR(MAX),
    DurationMinutes INT NOT NULL,
    RatingID INT FOREIGN KEY REFERENCES Ratings(ID) ON DELETE CASCADE,
    ReleaseDate DATE,
    LanguageID INT FOREIGN KEY REFERENCES Languages(ID) ON DELETE CASCADE,
    CountryID INT FOREIGN KEY REFERENCES Countries(ID) ON DELETE NO ACTION,
    DirectorID INT FOREIGN KEY REFERENCES Directors(ID) ON DELETE SET NULL,
    PosterImagePath NVARCHAR(500),
	WallpaperImagePath NVARCHAR(500),
	TrailerUrl NVARCHAR(500) NULL
);

-- MoviesActors (Junction Table)
CREATE TABLE MovieActors (
	ID INT IDENTITY PRIMARY KEY,
    MovieID INT NOT NULL,
    ActorID INT NOT NULL,
    Role NVARCHAR(255),
    FOREIGN KEY (MovieID) REFERENCES Movies(ID) ON DELETE CASCADE,
    FOREIGN KEY (ActorID) REFERENCES Actors(ID) ON DELETE CASCADE
);

-- MOVIEGENRE
CREATE TABLE MovieGenres (
    ID INT IDENTITY PRIMARY KEY,
    MovieID INT NOT NULL,
    GenreID INT NOT NULL,
    FOREIGN KEY (MovieID) REFERENCES Movies(ID) ON DELETE CASCADE,
    FOREIGN KEY (GenreID) REFERENCES Genres(ID) ON DELETE CASCADE,
    CONSTRAINT UC_MovieGenre UNIQUE (MovieID, GenreID)
);

-- MOVIEUSERFAVOURITES (Junction Table)
CREATE TABLE WatchList (
    ID INT IDENTITY PRIMARY KEY,
    UserID INT NOT NULL FOREIGN KEY REFERENCES Users(ID) ON DELETE CASCADE,
    MovieID INT NOT NULL FOREIGN KEY REFERENCES Movies(ID) ON DELETE CASCADE,
	AddedAt DATETIME DEFAULT GETDATE(),
    
    CONSTRAINT UC_MovieUserFavourites UNIQUE (UserID, MovieID)
);

-- SHOWTIMES
CREATE TABLE ShowTimes (
    ID INT IDENTITY PRIMARY KEY,
    MovieID INT NOT NULL,
    AuditoriumID INT NOT NULL,
    StartTime DATETIME NOT NULL,
    DurationMinutes INT NOT NULL,
    SubtitleLanguageID INT NULL,
    Is3D BIT NOT NULL DEFAULT 0,
	Price DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (MovieID) REFERENCES Movies(ID) ON DELETE CASCADE,
    FOREIGN KEY (AuditoriumID) REFERENCES Auditoriums(ID) ON DELETE CASCADE,
    FOREIGN KEY (SubtitleLanguageID) REFERENCES Languages(ID) -- removed ON DELETE CASCADE
);

-- TICKETS
CREATE TABLE Tickets (
    ID INT IDENTITY PRIMARY KEY,
    UserID INT FOREIGN KEY REFERENCES Users(ID) ON DELETE CASCADE,
    ShowTimeID INT FOREIGN KEY REFERENCES ShowTimes(ID) ON DELETE CASCADE,
    PurchaseTime DATETIME DEFAULT GETDATE(),
	Status NVARCHAR(20) DEFAULT 'Pending'
		CONSTRAINT CK_Tickets_Status CHECK (Status IN ('PENDING', 'PAID', 'CONFIRMED', 'CANCELLED'))
);

-- TICKET_SEATS
CREATE TABLE TicketSeats (
    ID INT IDENTITY PRIMARY KEY,
    TicketID INT NOT NULL,
    SeatID INT NOT NULL,
    FOREIGN KEY (TicketID) REFERENCES Tickets(ID) ON DELETE CASCADE,
    FOREIGN KEY (SeatID) REFERENCES Seats(ID)
);

-- TICKET PAYMENTS
CREATE TABLE TicketPayments (
    ID INT IDENTITY PRIMARY KEY,
    TicketID INT FOREIGN KEY REFERENCES Tickets(ID) ON DELETE CASCADE,

    Method VARCHAR(50) NOT NULL CHECK (
        Method IN (
            'CARD', 'AFTERPAY_CLEARPAY', 'ALIPAY', 'GRABPAY', 'IDEAL',
            'KONBINI', 'SEPA_DEBIT', 'SOFORT'
        )
    ),

    PaymentStatus VARCHAR(50) NOT NULL CHECK (
        PaymentStatus IN ('PENDING', 'COMPLETED', 'FAILED')
    ),

    PaidAmount DECIMAL(10, 2),
    PaidAt DATETIME
);

-- EMPLOYEE ROLES
CREATE TABLE EmployeeRoles (
    ID INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) UNIQUE NOT NULL,
    Description NVARCHAR(255)
);

-- EMPLOYEES
CREATE TABLE Employees (
    ID INT IDENTITY PRIMARY KEY,
    TheaterID INT FOREIGN KEY REFERENCES Theaters(ID) ON DELETE CASCADE,
    RoleID INT FOREIGN KEY REFERENCES EmployeeRoles(ID) ON DELETE CASCADE,
    FullName NVARCHAR(255) NOT NULL,
    Email NVARCHAR(255) UNIQUE NOT NULL,
    Phone NVARCHAR(20),
    Gender NVARCHAR(10) CHECK (Gender IN ('Male', 'Female', 'Other')),
    DateOfBirth DATE,
    CitizenID NVARCHAR(50) UNIQUE,
    Address NVARCHAR(255),
    HireDate DATE DEFAULT GETDATE(),
    Salary DECIMAL(10, 2) NOT NULL,
	ProfileImagePath NVARCHAR(500),
);

-- CONCESSIONS
CREATE TABLE Concessions (
    ID INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) UNIQUE NOT NULL,
    Description NVARCHAR(255),
    ImagePath NVARCHAR(500)
);

CREATE TABLE TheaterConcessions (
    ID INT IDENTITY PRIMARY KEY,
    TheaterID INT FOREIGN KEY REFERENCES Theaters(ID) ON DELETE CASCADE,
    ConcessionID INT FOREIGN KEY REFERENCES Concessions(ID) ON DELETE CASCADE,
    Price DECIMAL(6, 2) NOT NULL,
    StockLeft INT NOT NULL CHECK (StockLeft >= 0),
    IsAvailable BIT DEFAULT 1,
    CONSTRAINT UQ_Theater_Concession UNIQUE (TheaterID, ConcessionID)
);

-- ORDER ITEMS
CREATE TABLE OrderItems (
    ID INT IDENTITY PRIMARY KEY,
    TicketID INT FOREIGN KEY REFERENCES Tickets(ID) ON DELETE CASCADE,
    TheaterConcessionID INT FOREIGN KEY REFERENCES TheaterConcessions(ID) ON DELETE NO ACTION,
    Quantity INT NOT NULL CHECK (Quantity > 0),
    PriceAtPurchase DECIMAL(6, 2) NOT NULL
);

-- REVIEWS
CREATE TABLE Reviews (
    ID INT IDENTITY PRIMARY KEY,
    MovieID INT FOREIGN KEY REFERENCES Movies(ID) ON DELETE CASCADE,
    UserID INT FOREIGN KEY REFERENCES Users(ID) ON DELETE CASCADE,
    Rating INT CHECK (Rating BETWEEN 1 AND 5) NOT NULL,
    Comment NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    IsApproved BIT DEFAULT 0
);

-- ADMINS
CREATE TABLE Admins (
    ID INT IDENTITY PRIMARY KEY,
    EmployeeID INT UNIQUE NOT NULL,
    Username NVARCHAR(255) UNIQUE NOT NULL,
    FOREIGN KEY (EmployeeID) REFERENCES Employees(ID) ON DELETE CASCADE,
    PasswordHash NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);

CREATE TABLE LoginAttempts (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    AttemptTime DATETIME NOT NULL,
    IsSuccessful BIT NOT NULL,
    IPAddress NVARCHAR(45) NOT NULL,
    UserID INT NULL,
    AdminID INT NULL,

    -- Enforce mutual exclusivity: only one of these can be filled
    CONSTRAINT CK_LoginAttempts_OnlyOneUser CHECK (
        (UserID IS NOT NULL AND AdminID IS NULL) OR 
        (UserID IS NULL AND AdminID IS NOT NULL)
    ),

    -- Foreign Key Constraints
    FOREIGN KEY (UserID) REFERENCES Users(ID) ON DELETE CASCADE,
    FOREIGN KEY (AdminID) REFERENCES Admins(ID) ON DELETE CASCADE
);

CREATE TABLE Messages (
    ID INT IDENTITY(1,1) PRIMARY KEY,

    UserID INT NOT NULL, -- always present

    IsFromUser BIT NOT NULL, -- 1 = from customer, 0 = from mart

    MessageText VARBINARY(MAX) NOT NULL,

    SentAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    ReadAt DATETIME2 NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    IsDeletedBySender BIT NOT NULL DEFAULT 0,
    IsDeletedByReceiver BIT NOT NULL DEFAULT 0,

    CONSTRAINT FK_Messages_Users FOREIGN KEY (UserID) REFERENCES Users(ID) ON DELETE CASCADE
);

CREATE TABLE Notifications (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    
    -- Mutually exclusive columns
    UserID INT NULL,
    AdminID INT NULL,
    
    TicketID INT NULL, -- Foreign key to tickets, nullable if not linked to a sale
    Title NVARCHAR(255) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    IsRead BIT NOT NULL DEFAULT 0,
    NotificationType NVARCHAR(50) NOT NULL,

	MessageUserID INT NULL,
	MessageID INT NULL,
    
    -- Foreign Key Constraints
    FOREIGN KEY (UserID) REFERENCES Users(ID) ON DELETE CASCADE,
    FOREIGN KEY (AdminID) REFERENCES Admins(ID) ON DELETE CASCADE,
    FOREIGN KEY (TicketID) REFERENCES Tickets(ID),
    FOREIGN KEY (MessageUserID) REFERENCES Users(ID),
	FOREIGN KEY (MessageID) REFERENCES Messages(ID),

    -- 🔥 Mutually exclusive constraint:
    CONSTRAINT CHK_Notifications_Exclusivity CHECK (
        (UserID IS NOT NULL AND AdminID IS NULL) OR 
        (UserID IS NULL AND AdminID IS NOT NULL)
    ),
    
    -- 🔥 Restrict NotificationType to specific values
    CONSTRAINT CHK_Notifications_Type CHECK (NotificationType IN (
        'Account Related', 
        'Order Status Update', 
        'Security Alert',
		'New Message',
        'Promotion', 
        'System Message'
    ))
);
CREATE TABLE ReviewReactions (
    ID INT IDENTITY PRIMARY KEY,
    ReviewID INT NOT NULL,
    UserID INT NOT NULL,
    IsLike BIT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_ReviewReactions_Reviews FOREIGN KEY (ReviewID) 
        REFERENCES Reviews(ID),  -- Removed ON DELETE CASCADE
    CONSTRAINT FK_ReviewReactions_Users FOREIGN KEY (UserID) 
        REFERENCES Users(ID),    -- Removed ON DELETE CASCADE

    CONSTRAINT UC_ReviewReactions_UserReview UNIQUE (ReviewID, UserID)
);

GO
CREATE TRIGGER trg_DeleteReviewReactions_OnReviewDelete
ON Reviews
AFTER DELETE
AS
BEGIN
    DELETE FROM ReviewReactions
    WHERE ReviewID IN (SELECT ID FROM DELETED);
END;



GO

CREATE TRIGGER trg_DeleteOrderItemsOnTheaterConcessionDelete
ON TheaterConcessions
AFTER DELETE
AS
BEGIN
    DELETE FROM OrderItems
    WHERE TheaterConcessionID IN (SELECT ID FROM DELETED);
END;

GO

CREATE PROCEDURE sp_DeleteTheaterAndDependencies
    @TheaterID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Delete ShowTimes linked to the auditoriums of the theater
    DELETE FROM ShowTimes
    WHERE AuditoriumID IN (SELECT ID FROM Auditoriums WHERE TheaterID = @TheaterID);

    -- Delete TicketSeats linked to the seats in those auditoriums
    DELETE FROM TicketSeats
    WHERE SeatID IN (
        SELECT ID 
        FROM Seats 
        WHERE AuditoriumID IN (SELECT ID FROM Auditoriums WHERE TheaterID = @TheaterID)
    );

    -- Delete the seats
    DELETE FROM Seats
    WHERE AuditoriumID IN (SELECT ID FROM Auditoriums WHERE TheaterID = @TheaterID);

    -- Finally, delete the auditoriums
    DELETE FROM Auditoriums WHERE TheaterID = @TheaterID;

    -- And the theater itself
    DELETE FROM Theaters WHERE ID = @TheaterID;
END;
GO


CREATE PROCEDURE sp_DeleteSeatAndDependencies
    @SeatID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Delete TicketSeats linked to this seat
    DELETE FROM TicketSeats WHERE SeatID = @SeatID;

    -- Delete the seat itself
    DELETE FROM Seats WHERE ID = @SeatID;
END;
GO


-- Create stored procedure to delete Movies and Directors by Country
CREATE PROCEDURE sp_DeleteCountryMovies
    @CountryID INT
AS
BEGIN
    DELETE FROM Movies WHERE CountryID = @CountryID;
END;
GO

-- Create stored procedure to delete ShowTimes by Language
CREATE PROCEDURE sp_DeleteLanguage_ShowTimes
    @LanguageID INT
AS
BEGIN
    DELETE FROM ShowTimes WHERE SubtitleLanguageID = @LanguageID;
END;
GO

CREATE INDEX IX_Tickets_PurchaseTime_Status ON Tickets (PurchaseTime, Status);
CREATE INDEX IX_ShowTimes_StartTime_MovieID ON ShowTimes (StartTime, MovieID);
CREATE INDEX IX_ShowTimes_StartTime_AuditoriumID ON ShowTimes (StartTime, AuditoriumID);
CREATE INDEX IX_TicketSeats_TicketID ON TicketSeats (TicketID);
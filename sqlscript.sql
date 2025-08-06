use Fringe;

drop table if exists ShowTimes;
drop table if exists UserRatings;
drop table if exists Shows;
drop table if exists Ratings;
drop table if exists Venues;
drop table if exists ContentRatings;
drop table if exists Users;

GO

create table Users (
    Id int primary key identity(1,1) not null,
    Name varchar(100),
);

create table ContentRatings (
    Id int primary key identity(1,1) not null,
    Name varchar(50) not null,
    Code varchar(40) not null,
    Description text,
    constraint UQ_ContentRatings_Code unique (Code)
);

create table Venues (
    Id int primary key identity(1,1) not null,
    VenueNumber int not null,
    Name varchar(100) not null,
    Address text not null,
    Phone char(10) not null,
    PostalCode char(6) not null,
    CreatedAt datetime not null default current_timestamp,
    UpdatedAt datetime not null default current_timestamp,
    constraint UQ_Venues_VenueNumber unique (VenueNumber),
);

create table Shows (
    Id int primary key not null,
    Title varchar(255) not null,
    Description text,
    PlainTextDescription text,
    ImageUrl varchar(255),
    Tag varchar(50),
    Price decimal(10, 2) not null,
    Fee decimal(10, 2) not null,
    FirstShowDate date not null,
    LengthInMinutes int not null,
    VenueId int not null,
    ContentRatingId int not null,
    CreatedAt datetime not null default current_timestamp,
    UpdatedAt datetime not null default current_timestamp,
    foreign key (VenueId) references Venues(Id),
    foreign key (ContentRatingId) references ContentRatings(Id)
);

create table Ratings (
    Id int primary key identity(1,1) not null,
    Name varchar(50) not null,
    Code varchar(40) not null,
    Description text,
    constraint UQ_Ratings_Code unique (Code)
);

create table UserRatings (
    Id int primary key identity(1,1) not null,
    UserId int not null,
    ShowId int not null,
    RatingId int not null,
    CreatedAt datetime not null default current_timestamp,
    UpdatedAt datetime not null default current_timestamp,
    foreign key (UserId) references Users(Id),
    foreign key (ShowId) references Shows(Id),
    foreign key (RatingId) references Ratings(Id),
    constraint UQ_UserRatings unique (UserId, ShowId)
);

create table ShowTimes(
    Id int primary key identity(1,1) not null,
    ShowId int not null,
    DateTime datetime not null,
    PerformanceTime time not null,
    PerformanceDate varchar(50) not null,
    PresentationFormat varchar(20) not null,
    Reserved bit not null default 0,
    CreatedAt datetime not null default current_timestamp,
    UpdatedAt datetime not null default current_timestamp,
    foreign key (ShowId) references Shows(Id)
)
GO

insert into Ratings (Name, Code, Description)
VALUES
('Meh', 'MEH', 'Doesn''t really look good for me.'),
('Good', 'GOOD', 'I wanna see it.'),
('Great', 'GREAT', 'I really, really it!'),
('Must See', 'MUSTSEE', 'I will see it no matter what!');

insert into Users (Name)
values
('Jack'),
('Aaron'),
('Sam'),
('Adam'),
('Seth'),
('Christian'),
('Savanah')

GO
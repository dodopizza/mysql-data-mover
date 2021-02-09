CREATE SCHEMA test_db;
USE test_db;

DROP TABLE IF EXISTS `table_without_pk`;
-- CREATE TABLE `table_without_pk`
-- (
--     `Created` timestamp     NOT NULL DEFAULT CURRENT_TIMESTAMP,
--     `Message` varchar(1000)
-- ) ENGINE = InnoDB;


DROP TABLE IF EXISTS table_with_composite_pk;
-- CREATE TABLE `table_with_composite_pk`
-- (
--     `id_1`    bigint      NOT NULL,
--     `id_2`    bigint      NOT NULL,
--     `message` varchar(20) NOT NULL,
--     PRIMARY KEY (`id_1`, `id_2`)
-- ) ENGINE = InnoDB
--   DEFAULT CHARSET = utf8;

DELIMITER //
CREATE PROCEDURE fill_table_with_composite_pk(OUT records INT)
BEGIN
    SET @x = 0;
    outer_loop:
    REPEAT
        SET @y = 0;
        inner_loop:
        REPEAT
            INSERT INTO table_with_composite_pk (id_1, id_2, message)
            values (@x, @y, (@x * 100) + @y);
            SET @y = @y + 1;
        UNTIL @y >= 128 END REPEAT inner_loop;
        SET @x = @x + 1;
    UNTIL @x >= 8 END REPEAT outer_loop;

    SET @baseIndex = 1024;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;
    INSERT INTO table_with_composite_pk SELECT id_1 + @baseIndex as id_1, id_2, message from table_with_composite_pk;
    SET @baseIndex = @baseIndex * 2;

    SELECT count(*) AS records FROM table_with_composite_pk;
END //
DELIMITER ;

DELIMITER //
CREATE PROCEDURE fill_table_without_pk(OUT records INT)
BEGIN
    SET @z = 0;
    outer_loop:
    REPEAT
        INSERT INTO table_without_pk (Created, Message)
        VALUE (NOW(), CONCAT('Row', @z));
        SET @z = @z + 1;
    UNTIL @z >= 1000 END REPEAT outer_loop;

    SELECT count(*) AS records FROM table_without_pk;
END //
DELIMITER ;

DELIMITER //
CREATE PROCEDURE fill(OUT status CHAR)
BEGIN
    -- CALL fill_table_without_pk(@records);
    -- CALL fill_table_with_composite_pk(@records);
    SELECT 'INIT COMPLETE' AS status;
    CREATE USER 'ready'@'%' IDENTIFIED BY 'ready';
END//
DELIMITER ;

CALL fill(@status);
